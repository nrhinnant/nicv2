using System.Security.Principal;
using Microsoft.Extensions.Configuration;
using WfpTrafficControl.Service.Ipc;
using WfpTrafficControl.Service.Wfp;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Lkg;
using WfpTrafficControl.Shared.Native;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Service;

/// <summary>
/// Background worker service that manages the WFP policy controller lifecycle.
/// Currently a placeholder that demonstrates clean start/stop behavior.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;
    private readonly IWfpEngine _wfpEngine;
    private PipeServer? _pipeServer;
    private PolicyFileWatcher? _fileWatcher;

    public Worker(ILogger<Worker> logger, ILoggerFactory loggerFactory, IConfiguration configuration)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _configuration = configuration;

        // Create WfpInterop for production use (wraps native P/Invoke calls)
        var wfpInterop = new WfpInterop(_loggerFactory.CreateLogger<WfpInterop>());
        _wfpEngine = new WfpEngine(_loggerFactory.CreateLogger<WfpEngine>(), wfpInterop);
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Check elevation BEFORE any WFP operations or IPC server start.
        // WFP APIs require Administrator privileges; running without them would
        // cause cryptic ERROR_ACCESS_DENIED failures later.
        if (!IsRunningAsAdministrator())
        {
            _logger.LogCritical(
                "Service must run with Administrator privileges to access Windows Filtering Platform");

            // Throw to prevent the service from starting. The Generic Host will
            // catch this, log the error, and exit with a non-zero exit code.
            throw new InvalidOperationException(
                "Service must run with Administrator privileges to access Windows Filtering Platform");
        }

        var version = GetVersion();

        _logger.LogInformation(
            "{ServiceName} v{Version} starting at {Time}",
            WfpConstants.ServiceName,
            version,
            DateTimeOffset.Now);

        // Initialize file watcher with configuration
        _fileWatcher = new PolicyFileWatcher(
            _loggerFactory.CreateLogger<PolicyFileWatcher>(),
            _wfpEngine);

        // Configure debounce from appsettings
        var debounceMs = _configuration.GetValue<int>("WfpTrafficControl:FileWatch:DebounceMs",
            PolicyFileWatcher.DefaultDebounceMs);
        if (debounceMs >= PolicyFileWatcher.MinDebounceMs && debounceMs <= PolicyFileWatcher.MaxDebounceMs)
        {
            _fileWatcher.SetDebounceMs(debounceMs);
            _logger.LogDebug("File watch debounce set to {DebounceMs}ms", debounceMs);
        }

        // Start the IPC pipe server (pass file watcher for control)
        _pipeServer = new PipeServer(
            _loggerFactory.CreateLogger<PipeServer>(),
            version,
            _wfpEngine,
            _fileWatcher);
        _pipeServer.Start();

        // Auto-apply LKG policy on startup if enabled via configuration.
        // Default is false (fail-open) - operator must explicitly enable this.
        // Set "WfpTrafficControl:AutoApplyLkgOnStartup" to true in appsettings.json to enable.
        var autoApplyLkg = _configuration.GetValue<bool>("WfpTrafficControl:AutoApplyLkgOnStartup", false);
        if (autoApplyLkg)
        {
            ApplyLkgOnStartup();
        }
        else
        {
            _logger.LogDebug("Auto-apply LKG on startup is disabled (default fail-open behavior)");
        }

        return base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Attempts to apply the LKG policy on service startup.
    /// Implements fail-open behavior: if LKG is missing or corrupt, do nothing.
    /// </summary>
    private void ApplyLkgOnStartup()
    {
        _logger.LogInformation("Checking for LKG policy to auto-apply on startup");

        try
        {
            // Load the LKG policy
            var loadResult = LkgStore.Load();

            if (!loadResult.Exists)
            {
                if (loadResult.Error != null)
                {
                    // LKG exists but is corrupt - fail open
                    _logger.LogWarning(
                        "LKG policy is corrupt or invalid (fail-open, not applying): {Error}",
                        loadResult.Error);
                }
                else
                {
                    // No LKG exists - this is normal for first run
                    _logger.LogInformation("No LKG policy found, starting with no policy (fail-open)");
                }
                return;
            }

            var policy = loadResult.Policy!;
            _logger.LogInformation(
                "LKG policy found: version={Version}, rules={RuleCount}",
                policy.Version, policy.Rules.Count);

            // Compile the policy
            var compilationResult = RuleCompiler.Compile(policy);
            if (!compilationResult.IsSuccess)
            {
                _logger.LogWarning(
                    "LKG policy compilation failed (fail-open, not applying): {ErrorCount} error(s)",
                    compilationResult.Errors.Count);
                return;
            }

            _logger.LogInformation(
                "LKG policy compiled: {FilterCount} filter(s), {SkippedCount} rule(s) skipped",
                compilationResult.Filters.Count, compilationResult.SkippedRules);

            // Apply the compiled filters
            var applyResult = _wfpEngine.ApplyFilters(compilationResult.Filters);
            if (applyResult.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to apply LKG policy on startup (fail-open): {Error}",
                    applyResult.Error);
                return;
            }

            _logger.LogInformation(
                "LKG policy applied on startup: {Created} filter(s) created, {Removed} filter(s) removed",
                applyResult.Value.FiltersCreated, applyResult.Value.FiltersRemoved);
        }
        catch (Exception ex)
        {
            // Catch-all for unexpected errors - fail open
            _logger.LogWarning(ex,
                "Unexpected error during LKG auto-apply on startup (fail-open, not applying)");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{ServiceName} is now running", WfpConstants.ServiceName);

        try
        {
            // Main service loop
            while (!stoppingToken.IsCancellationRequested)
            {
                // Placeholder: WFP policy controller logic will go here
                // For now, just idle and stay responsive to stop signals
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested - not an error
            _logger.LogDebug("Service execution cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in service execution");
            // Don't rethrow - let the service stop gracefully
            // The lifetime will be stopped in StopAsync
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{ServiceName} stopping at {Time}",
            WfpConstants.ServiceName,
            DateTimeOffset.Now);

        // Stop file watching
        if (_fileWatcher != null)
        {
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }

        // Stop the IPC pipe server
        if (_pipeServer != null)
        {
            await _pipeServer.StopAsync();
            _pipeServer.Dispose();
            _pipeServer = null;
        }

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("{ServiceName} stopped", WfpConstants.ServiceName);
    }

    private static string GetVersion()
    {
        var assembly = typeof(Worker).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "0.0.0";
    }

    /// <summary>
    /// Checks if the current process is running with Administrator privileges.
    /// </summary>
    /// <returns>True if running as Administrator, false otherwise.</returns>
    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
