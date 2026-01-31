using Microsoft.Extensions.Logging;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Lkg;
using WfpTrafficControl.Shared.Native;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Service;

/// <summary>
/// Watches a policy file for changes and automatically reapplies on modifications.
/// Implements debouncing to avoid rapid reapplies during file edits.
/// </summary>
public sealed class PolicyFileWatcher : IDisposable
{
    private readonly ILogger<PolicyFileWatcher> _logger;
    private readonly IWfpEngine _wfpEngine;
    private readonly object _lock = new();

    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCts;
    private string? _watchedPath;
    private bool _disposed;

    // Configuration
    private int _debounceMs = DefaultDebounceMs;
    public const int DefaultDebounceMs = 1000;
    public const int MinDebounceMs = 100;
    public const int MaxDebounceMs = 30000;
    private const int FileReadRetryCount = 3;
    private const int FileReadRetryDelayMs = 100;

    // Statistics
    private DateTime? _lastApplyTime;
    private string? _lastError;
    private DateTime? _lastErrorTime;
    private int _applyCount;
    private int _errorCount;

    public PolicyFileWatcher(ILogger<PolicyFileWatcher> logger, IWfpEngine wfpEngine)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _wfpEngine = wfpEngine ?? throw new ArgumentNullException(nameof(wfpEngine));
    }

    /// <summary>
    /// Gets whether file watching is currently active.
    /// </summary>
    public bool IsWatching
    {
        get
        {
            lock (_lock)
            {
                return _watcher != null && _watchedPath != null;
            }
        }
    }

    /// <summary>
    /// Gets the path being watched (null if not watching).
    /// </summary>
    public string? WatchedPath
    {
        get
        {
            lock (_lock)
            {
                return _watchedPath;
            }
        }
    }

    /// <summary>
    /// Gets the debounce interval in milliseconds.
    /// </summary>
    public int DebounceMs
    {
        get
        {
            lock (_lock)
            {
                return _debounceMs;
            }
        }
    }

    /// <summary>
    /// Gets the time of the last successful apply.
    /// </summary>
    public DateTime? LastApplyTime
    {
        get
        {
            lock (_lock)
            {
                return _lastApplyTime;
            }
        }
    }

    /// <summary>
    /// Gets the last error message (if any).
    /// </summary>
    public string? LastError
    {
        get
        {
            lock (_lock)
            {
                return _lastError;
            }
        }
    }

    /// <summary>
    /// Gets the time of the last error.
    /// </summary>
    public DateTime? LastErrorTime
    {
        get
        {
            lock (_lock)
            {
                return _lastErrorTime;
            }
        }
    }

    /// <summary>
    /// Gets the number of successful applies since watching started.
    /// </summary>
    public int ApplyCount
    {
        get
        {
            lock (_lock)
            {
                return _applyCount;
            }
        }
    }

    /// <summary>
    /// Gets the number of failed applies since watching started.
    /// </summary>
    public int ErrorCount
    {
        get
        {
            lock (_lock)
            {
                return _errorCount;
            }
        }
    }

    /// <summary>
    /// Sets the debounce interval.
    /// </summary>
    public void SetDebounceMs(int debounceMs)
    {
        if (debounceMs < MinDebounceMs || debounceMs > MaxDebounceMs)
        {
            throw new ArgumentOutOfRangeException(nameof(debounceMs),
                $"Debounce must be between {MinDebounceMs} and {MaxDebounceMs} ms.");
        }

        lock (_lock)
        {
            _debounceMs = debounceMs;
        }
    }

    /// <summary>
    /// Starts watching a policy file. Applies the policy immediately.
    /// </summary>
    /// <param name="policyPath">The absolute path to the policy file.</param>
    /// <returns>Result indicating success/failure and whether initial apply succeeded.</returns>
    public Result<WatchStartResult> StartWatching(string policyPath)
    {
        if (string.IsNullOrWhiteSpace(policyPath))
        {
            return Result<WatchStartResult>.Failure(ErrorCodes.InvalidArgument, "Policy path is required.");
        }

        // Security: Check for path traversal
        if (policyPath.Contains(".."))
        {
            return Result<WatchStartResult>.Failure(ErrorCodes.InvalidArgument,
                "Policy path cannot contain '..' (path traversal).");
        }

        // Validate path is absolute
        if (!Path.IsPathRooted(policyPath))
        {
            return Result<WatchStartResult>.Failure(ErrorCodes.InvalidArgument,
                "Policy path must be absolute.");
        }

        // Check file exists
        if (!File.Exists(policyPath))
        {
            return Result<WatchStartResult>.Failure(ErrorCodes.InvalidArgument,
                $"Policy file not found: {policyPath}");
        }

        lock (_lock)
        {
            // Stop any existing watcher
            StopWatchingInternal();

            // Reset statistics
            _lastApplyTime = null;
            _lastError = null;
            _lastErrorTime = null;
            _applyCount = 0;
            _errorCount = 0;

            try
            {
                // Get directory and filename
                var directory = Path.GetDirectoryName(policyPath);
                var filename = Path.GetFileName(policyPath);

                if (string.IsNullOrEmpty(directory))
                {
                    return Result<WatchStartResult>.Failure(ErrorCodes.InvalidArgument,
                        "Could not determine directory from policy path.");
                }

                // Create FileSystemWatcher
                _watcher = new FileSystemWatcher(directory, filename)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = false // Enable after setup
                };

                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileChanged;
                _watcher.Renamed += OnFileRenamed;
                _watcher.Error += OnWatcherError;

                _watchedPath = policyPath;

                _logger.LogInformation("Starting file watch on: {Path}", policyPath);

                // Apply immediately
                var applyResult = ApplyPolicyInternal(policyPath);
                bool initialApplySuccess = applyResult.IsSuccess;
                string? warning = null;

                if (!initialApplySuccess)
                {
                    warning = applyResult.Error.Message;
                    _logger.LogWarning("Initial policy apply failed (fail-open, watching anyway): {Error}",
                        applyResult.Error.Message);
                }

                // Start watching
                _watcher.EnableRaisingEvents = true;

                return Result<WatchStartResult>.Success(new WatchStartResult
                {
                    InitialApplySuccess = initialApplySuccess,
                    Warning = warning
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start file watcher for: {Path}", policyPath);
                StopWatchingInternal();
                return Result<WatchStartResult>.Failure(ErrorCodes.ServiceError,
                    $"Failed to start file watcher: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Stops watching the policy file.
    /// </summary>
    public void StopWatching()
    {
        lock (_lock)
        {
            if (_watchedPath != null)
            {
                _logger.LogInformation("Stopping file watch on: {Path}", _watchedPath);
            }
            StopWatchingInternal();
        }
    }

    private void StopWatchingInternal()
    {
        // Cancel any pending debounce
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;

        // Dispose watcher
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Error -= OnWatcherError;
            _watcher.Dispose();
            _watcher = null;
        }

        _watchedPath = null;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("File change detected: {ChangeType} on {Path}", e.ChangeType, e.FullPath);
        ScheduleDebounceApply();
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogDebug("File renamed: {OldName} to {NewName}", e.OldFullPath, e.FullPath);

        // If renamed back to our watched filename, trigger apply
        lock (_lock)
        {
            if (_watchedPath != null &&
                string.Equals(e.FullPath, _watchedPath, StringComparison.OrdinalIgnoreCase))
            {
                ScheduleDebounceApply();
            }
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        _logger.LogError(ex, "FileSystemWatcher error");

        lock (_lock)
        {
            _lastError = $"Watcher error: {ex.Message}";
            _lastErrorTime = DateTime.UtcNow;
            _errorCount++;
        }
    }

    private void ScheduleDebounceApply()
    {
        lock (_lock)
        {
            if (_watchedPath == null)
            {
                return;
            }

            // Cancel any existing debounce timer
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();

            var cts = _debounceCts;
            var path = _watchedPath;
            var debounce = _debounceMs;

            // Start new debounce timer
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(debounce, cts.Token);

                    // Debounce completed, apply the policy
                    lock (_lock)
                    {
                        if (_watchedPath == path && !cts.Token.IsCancellationRequested)
                        {
                            _logger.LogInformation("Debounce completed, applying policy: {Path}", path);
                            ApplyPolicyInternal(path);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Debounce was cancelled (new change came in or watcher stopped)
                    _logger.LogDebug("Debounce cancelled for: {Path}", path);
                }
            });
        }
    }

    /// <summary>
    /// Applies the policy file. Must be called under lock.
    /// Implements fail-open: on failure, keeps last applied policy.
    /// </summary>
    private Result<Unit> ApplyPolicyInternal(string policyPath)
    {
        try
        {
            // Step 1: Read file with retry (file may still be locked by editor)
            string json;
            var readResult = ReadFileWithRetry(policyPath);
            if (readResult.IsFailure)
            {
                RecordError(readResult.Error.Message);
                return Result<Unit>.Failure(readResult.Error.Code, readResult.Error.Message);
            }
            json = readResult.Value;

            // Step 2: Validate the policy
            var validationResult = PolicyValidator.ValidateJson(json);
            if (!validationResult.IsValid)
            {
                var error = $"Policy validation failed: {validationResult.GetSummary()}";
                RecordError(error);
                return Result<Unit>.Failure(ErrorCodes.InvalidArgument, error);
            }

            // Step 3: Parse the policy
            var policy = Policy.FromJson(json);
            if (policy == null)
            {
                var error = "Failed to parse policy after validation";
                RecordError(error);
                return Result<Unit>.Failure(ErrorCodes.InvalidArgument, error);
            }

            _logger.LogInformation("Policy loaded from watch: version={Version}, rules={RuleCount}",
                policy.Version, policy.Rules.Count);

            // Step 4: Compile to WFP filters
            var compilationResult = RuleCompiler.Compile(policy);
            if (!compilationResult.IsSuccess)
            {
                var error = $"Policy compilation failed: {compilationResult.Errors.Count} error(s)";
                RecordError(error);
                return Result<Unit>.Failure(ErrorCodes.InvalidArgument, error);
            }

            _logger.LogInformation("Policy compiled: {FilterCount} filter(s), {SkippedCount} rule(s) skipped",
                compilationResult.Filters.Count, compilationResult.SkippedRules);

            // Step 5: Apply to WFP
            var applyResult = _wfpEngine.ApplyFilters(compilationResult.Filters);
            if (applyResult.IsFailure)
            {
                var error = $"Failed to apply filters: {applyResult.Error.Message}";
                RecordError(error);
                return Result<Unit>.Failure(applyResult.Error.Code, error);
            }

            _logger.LogInformation("Policy applied via watch: {Created} filter(s) created, {Removed} filter(s) removed",
                applyResult.Value.FiltersCreated, applyResult.Value.FiltersRemoved);

            // Step 6: Save as LKG (non-fatal if fails)
            var lkgResult = LkgStore.Save(json, policyPath);
            if (lkgResult.IsFailure)
            {
                _logger.LogWarning("Failed to save LKG policy (non-fatal): {Error}", lkgResult.Error);
            }

            // Record success
            _lastApplyTime = DateTime.UtcNow;
            _applyCount++;
            // Clear last error on success
            _lastError = null;
            _lastErrorTime = null;

            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error during policy apply: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during watched policy apply");
            RecordError(error);
            return Result<Unit>.Failure(ErrorCodes.ServiceError, error);
        }
    }

    private void RecordError(string error)
    {
        _lastError = error;
        _lastErrorTime = DateTime.UtcNow;
        _errorCount++;
        _logger.LogWarning("Policy apply failed (fail-open, keeping last applied): {Error}", error);
    }

    private Result<string> ReadFileWithRetry(string path)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt < FileReadRetryCount; attempt++)
        {
            try
            {
                // Check file size
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length > PolicyValidator.MaxPolicyFileSize)
                {
                    return Result<string>.Failure(ErrorCodes.InvalidArgument,
                        $"Policy file exceeds maximum size ({PolicyValidator.MaxPolicyFileSize / 1024} KB)");
                }

                // Read file
                var content = File.ReadAllText(path);
                return Result<string>.Success(content);
            }
            catch (IOException ex) when (attempt < FileReadRetryCount - 1)
            {
                // File may be locked, retry after delay
                lastException = ex;
                _logger.LogDebug("File read attempt {Attempt} failed, retrying: {Error}",
                    attempt + 1, ex.Message);
                Thread.Sleep(FileReadRetryDelayMs);
            }
            catch (Exception ex)
            {
                return Result<string>.Failure(ErrorCodes.ServiceError,
                    $"Failed to read policy file: {ex.Message}");
            }
        }

        return Result<string>.Failure(ErrorCodes.ServiceError,
            $"Failed to read policy file after {FileReadRetryCount} attempts: {lastException?.Message}");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopWatching();
    }
}

/// <summary>
/// Result of starting file watching.
/// </summary>
public sealed class WatchStartResult
{
    /// <summary>
    /// Whether the initial policy apply succeeded.
    /// </summary>
    public bool InitialApplySuccess { get; set; }

    /// <summary>
    /// Warning message if initial apply failed (non-fatal).
    /// </summary>
    public string? Warning { get; set; }
}

/// <summary>
/// Represents a void/unit type for Result{T}.
/// </summary>
public readonly struct Unit
{
    public static readonly Unit Value = new();
}
