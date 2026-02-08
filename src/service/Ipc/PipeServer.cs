using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Logging;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Audit;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.Shared.Lkg;
using WfpTrafficControl.Shared.Native;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Service.Ipc;

/// <summary>
/// Named pipe server for CLI-to-service IPC communication.
/// Handles authorization, message parsing, and request dispatch.
/// </summary>
public sealed class PipeServer : IDisposable
{
    private readonly ILogger<PipeServer> _logger;
    private readonly string _serviceVersion;
    private readonly IWfpEngine _wfpEngine;
    private readonly PolicyFileWatcher _fileWatcher;
    private readonly IAuditLogWriter _auditLog;
    private readonly AuditLogReader _auditLogReader;
    private readonly CancellationTokenSource _cts;
    private Task? _listenerTask;
    private bool _disposed;

    /// <summary>
    /// Well-known SID for LocalSystem account.
    /// </summary>
    private static readonly SecurityIdentifier LocalSystemSid = new(WellKnownSidType.LocalSystemSid, null);

    /// <summary>
    /// Well-known SID for Administrators group.
    /// </summary>
    private static readonly SecurityIdentifier AdministratorsSid = new(WellKnownSidType.BuiltinAdministratorsSid, null);

    /// <summary>
    /// Creates a PipeSecurity object that restricts access to Administrators and LocalSystem only.
    /// This provides OS-level access control in addition to application-level authorization checks.
    /// </summary>
    private static PipeSecurity CreatePipeSecurity()
    {
        var security = new PipeSecurity();

        // Allow LocalSystem (the service account) full control
        security.AddAccessRule(new PipeAccessRule(
            LocalSystemSid,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // Allow Administrators full control (for CLI access)
        security.AddAccessRule(new PipeAccessRule(
            AdministratorsSid,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return security;
    }

    public PipeServer(ILogger<PipeServer> logger, string serviceVersion, IWfpEngine wfpEngine, PolicyFileWatcher fileWatcher, IAuditLogWriter? auditLog = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceVersion = serviceVersion ?? throw new ArgumentNullException(nameof(serviceVersion));
        _wfpEngine = wfpEngine ?? throw new ArgumentNullException(nameof(wfpEngine));
        _fileWatcher = fileWatcher ?? throw new ArgumentNullException(nameof(fileWatcher));
        _auditLog = auditLog ?? new AuditLogWriter();
        _auditLogReader = new AuditLogReader(_auditLog.LogPath);
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Starts the pipe server listening loop.
    /// </summary>
    public void Start()
    {
        if (_listenerTask != null)
        {
            throw new InvalidOperationException("Pipe server is already running.");
        }

        _logger.LogInformation("Starting pipe server on {PipeName}", WfpConstants.PipeFullPath);
        _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Stops the pipe server and waits for it to complete.
    /// </summary>
    public async Task StopAsync()
    {
        if (_listenerTask == null)
        {
            return;
        }

        _logger.LogInformation("Stopping pipe server");
        _cts.Cancel();

        try
        {
            // Give the listener a chance to exit gracefully
            await _listenerTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Pipe server did not stop within timeout");
        }

        _listenerTask = null;
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        // Create pipe security ACL once (reused for each connection)
        var pipeSecurity = CreatePipeSecurity();

        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipeServer = null;

            try
            {
                // Create a new pipe server for each connection with security ACL
                // This restricts access at the OS level to Administrators and LocalSystem
                pipeServer = NamedPipeServerStreamAcl.Create(
                    WfpConstants.PipeName,
                    PipeDirection.InOut,
                    1, // maxNumberOfServerInstances - one at a time (sequential)
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: WfpConstants.IpcMaxMessageSize,
                    outBufferSize: WfpConstants.IpcMaxMessageSize,
                    pipeSecurity);

                _logger.LogDebug("Waiting for client connection on {PipeName}", WfpConstants.PipeName);

                // Wait for a client to connect
                await pipeServer.WaitForConnectionAsync(cancellationToken);

                _logger.LogDebug("Client connected to pipe");

                // Handle the connection
                await HandleConnectionAsync(pipeServer, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Server is shutting down
                _logger.LogDebug("Pipe server listen loop cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in pipe server listen loop");
                // Continue listening for new connections after error
            }
            finally
            {
                if (pipeServer != null)
                {
                    try
                    {
                        if (pipeServer.IsConnected)
                        {
                            pipeServer.Disconnect();
                        }
                    }
                    catch
                    {
                        // Ignore disconnect errors
                    }

                    await pipeServer.DisposeAsync();
                }
            }
        }

        _logger.LogInformation("Pipe server listen loop ended");
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Read the request with timeout
            // NOTE: On Windows, we must read data from the pipe before we can impersonate the client.
            // Therefore, we read the request first, then authorize, then process.
            var request = await ReadRequestAsync(pipeServer, cancellationToken);
            if (request == null)
            {
                // Error already logged and response sent in ReadRequestAsync
                return;
            }

            // Step 2: Authorize the client (must happen after reading data from pipe)
            if (!AuthorizeClient(pipeServer))
            {
                _logger.LogWarning("Unauthorized client connection rejected");
                await SendResponseAsync(pipeServer, ErrorResponse.AccessDenied(), cancellationToken);
                return;
            }

            // Step 3: Validate protocol version
            var versionError = IpcMessageParser.ValidateProtocolVersion(request);
            if (versionError != null)
            {
                _logger.LogWarning("Protocol version mismatch: client={ClientVersion}, supported={MinVersion}-{MaxVersion}",
                    request.ProtocolVersion, WfpConstants.IpcMinProtocolVersion, WfpConstants.IpcProtocolVersion);
                await SendResponseAsync(pipeServer, versionError, cancellationToken);
                return;
            }

            // Log warning for clients not sending protocol version (backward compatibility)
            if (request.ProtocolVersion == 0)
            {
                _logger.LogDebug("Client did not send protocol version (backward compatibility mode)");
            }

            // Step 4: Process the request
            var response = ProcessRequest(request);

            // Step 5: Send the response
            await SendResponseAsync(pipeServer, response, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Connection handling cancelled
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling pipe connection");

            try
            {
                await SendResponseAsync(pipeServer, new ErrorResponse($"Internal error: {ex.Message}"), cancellationToken);
            }
            catch
            {
                // Ignore errors sending error response
            }
        }
    }

    /// <summary>
    /// Authorizes the connected client by checking if they are a local administrator or LocalSystem.
    /// </summary>
    /// <returns>True if authorized, false otherwise.</returns>
    private bool AuthorizeClient(NamedPipeServerStream pipeServer)
    {
        try
        {
            // Get the client's username for logging
            var clientUserName = pipeServer.GetImpersonationUserName();
            _logger.LogDebug("Client connected as: {ClientUserName}", clientUserName);

            // Check if the client is an administrator by impersonating them
            // and checking their group membership
            bool isAuthorized = false;

            pipeServer.RunAsClient(() =>
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);

                // Check if user is LocalSystem
                if (identity.User != null && identity.User.Equals(LocalSystemSid))
                {
                    _logger.LogDebug("Client is LocalSystem - authorized");
                    isAuthorized = true;
                    return;
                }

                // Check if user is in Administrators group
                if (principal.IsInRole(AdministratorsSid))
                {
                    _logger.LogDebug("Client is in Administrators group - authorized");
                    isAuthorized = true;
                    return;
                }

                _logger.LogDebug("Client is not authorized (not admin, not LocalSystem)");
            });

            return isAuthorized;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during client authorization");
            return false;
        }
    }

    private async Task<IpcRequest?> ReadRequestAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
    {
        try
        {
            // Create a timeout for the read operation
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(WfpConstants.IpcReadTimeoutMs);

            // Read length prefix (4 bytes, little-endian)
            var lengthBuffer = new byte[4];
            var bytesRead = await ReadExactlyAsync(pipeServer, lengthBuffer, 0, 4, timeoutCts.Token);
            if (bytesRead < 4)
            {
                _logger.LogWarning("Client disconnected before sending complete request length");
                return null;
            }

            var messageLength = BitConverter.ToInt32(lengthBuffer, 0);

            // Validate message length using centralized validation
            var sizeError = IpcMessageParser.ValidateMessageSize(messageLength);
            if (sizeError != null)
            {
                _logger.LogWarning("Invalid message length: {MessageLength}", messageLength);
                await SendResponseAsync(pipeServer, sizeError, cancellationToken);
                return null;
            }

            // Read the message body
            var messageBuffer = new byte[messageLength];
            bytesRead = await ReadExactlyAsync(pipeServer, messageBuffer, 0, messageLength, timeoutCts.Token);
            if (bytesRead < messageLength)
            {
                _logger.LogWarning("Client disconnected before sending complete message");
                return null;
            }

            var json = Encoding.UTF8.GetString(messageBuffer);
            _logger.LogDebug("Received request: {Request}", json);

            // Parse the request
            var parseResult = IpcMessageParser.ParseRequest(json);
            if (parseResult.IsFailure)
            {
                _logger.LogWarning("Failed to parse request: {Error}", parseResult.Error);
                await SendResponseAsync(pipeServer, new ErrorResponse(parseResult.Error.Message), cancellationToken);
                return null;
            }

            return parseResult.Value;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Read timeout
            _logger.LogWarning("Read timeout waiting for client request");
            await SendResponseAsync(pipeServer, new ErrorResponse("Request timeout"), cancellationToken);
            return null;
        }
    }

    private static async Task<int> ReadExactlyAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead), cancellationToken);
            if (read == 0)
            {
                // End of stream
                break;
            }
            totalRead += read;
        }
        return totalRead;
    }

    private IpcResponse ProcessRequest(IpcRequest request)
    {
        _logger.LogDebug("Processing request type: {RequestType}", request.Type);

        return request switch
        {
            PingRequest => PingResponse.Success(_serviceVersion),
            BootstrapRequest => ProcessBootstrapRequest(),
            TeardownRequest => ProcessTeardownRequest(),
            DemoBlockEnableRequest => ProcessDemoBlockEnableRequest(),
            DemoBlockDisableRequest => ProcessDemoBlockDisableRequest(),
            DemoBlockStatusRequest => ProcessDemoBlockStatusRequest(),
            RollbackRequest => ProcessRollbackRequest(),
            ValidateRequest validateRequest => ProcessValidateRequest(validateRequest),
            ApplyRequest applyRequest => ProcessApplyRequest(applyRequest),
            LkgShowRequest => ProcessLkgShowRequest(),
            LkgRevertRequest => ProcessLkgRevertRequest(),
            WatchSetRequest watchSetRequest => ProcessWatchSetRequest(watchSetRequest),
            WatchStatusRequest => ProcessWatchStatusRequest(),
            AuditLogsRequest auditLogsRequest => ProcessAuditLogsRequest(auditLogsRequest),
            _ => new ErrorResponse($"Unknown request type: {request.Type}")
        };
    }

    private IpcResponse ProcessBootstrapRequest()
    {
        _logger.LogInformation("Processing bootstrap request");

        var result = _wfpEngine.EnsureProviderAndSublayerExist();
        if (result.IsFailure)
        {
            _logger.LogError("Bootstrap failed: {Error}", result.Error);
            return BootstrapResponse.Failure(result.Error.Message);
        }

        // Check current state to report what exists
        var providerExists = _wfpEngine.ProviderExists();
        var sublayerExists = _wfpEngine.SublayerExists();

        return BootstrapResponse.Success(
            providerExists.IsSuccess && providerExists.Value,
            sublayerExists.IsSuccess && sublayerExists.Value);
    }

    private IpcResponse ProcessTeardownRequest()
    {
        _logger.LogInformation("Processing teardown request");
        _auditLog.Write(AuditLogEntry.TeardownStarted(AuditSource.Cli));

        // Check what exists before removal for reporting
        var providerExistedBefore = _wfpEngine.ProviderExists();
        var sublayerExistedBefore = _wfpEngine.SublayerExists();

        var result = _wfpEngine.RemoveProviderAndSublayer();
        if (result.IsFailure)
        {
            _logger.LogError("Teardown failed: {Error}", result.Error);
            _auditLog.Write(AuditLogEntry.TeardownFailed(AuditSource.Cli, "WFP_ERROR", result.Error.Message));
            return TeardownResponse.Failure(result.Error.Message);
        }

        var providerRemoved = providerExistedBefore.IsSuccess && providerExistedBefore.Value;
        var sublayerRemoved = sublayerExistedBefore.IsSuccess && sublayerExistedBefore.Value;

        _auditLog.Write(AuditLogEntry.TeardownFinished(AuditSource.Cli, providerRemoved, sublayerRemoved));
        return TeardownResponse.Success(providerRemoved, sublayerRemoved);
    }

    private IpcResponse ProcessDemoBlockEnableRequest()
    {
        _logger.LogInformation("Processing demo-block enable request");

        // Ensure provider/sublayer exist first
        var bootstrapResult = _wfpEngine.EnsureProviderAndSublayerExist();
        if (bootstrapResult.IsFailure)
        {
            _logger.LogError("Bootstrap failed during demo-block enable: {Error}", bootstrapResult.Error);
            return DemoBlockEnableResponse.Failure($"Bootstrap failed: {bootstrapResult.Error.Message}");
        }

        // Add the demo block filter
        var result = _wfpEngine.AddDemoBlockFilter();
        if (result.IsFailure)
        {
            _logger.LogError("Demo block enable failed: {Error}", result.Error);
            return DemoBlockEnableResponse.Failure(result.Error.Message);
        }

        // Verify it's active
        var existsResult = _wfpEngine.DemoBlockFilterExists();
        return DemoBlockEnableResponse.Success(existsResult.IsSuccess && existsResult.Value);
    }

    private IpcResponse ProcessDemoBlockDisableRequest()
    {
        _logger.LogInformation("Processing demo-block disable request");

        var result = _wfpEngine.RemoveDemoBlockFilter();
        if (result.IsFailure)
        {
            _logger.LogError("Demo block disable failed: {Error}", result.Error);
            return DemoBlockDisableResponse.Failure(result.Error.Message);
        }

        // Verify it's gone
        var existsResult = _wfpEngine.DemoBlockFilterExists();
        return DemoBlockDisableResponse.Success(!existsResult.IsSuccess || !existsResult.Value);
    }

    private IpcResponse ProcessDemoBlockStatusRequest()
    {
        _logger.LogInformation("Processing demo-block status request");

        var existsResult = _wfpEngine.DemoBlockFilterExists();
        if (existsResult.IsFailure)
        {
            _logger.LogError("Demo block status check failed: {Error}", existsResult.Error);
            return DemoBlockStatusResponse.Failure(existsResult.Error.Message);
        }

        return DemoBlockStatusResponse.Success(existsResult.Value);
    }

    private IpcResponse ProcessRollbackRequest()
    {
        _logger.LogInformation("Processing rollback request (panic rollback)");
        _auditLog.Write(AuditLogEntry.RollbackStarted(AuditSource.Cli));

        var result = _wfpEngine.RemoveAllFilters();
        if (result.IsFailure)
        {
            _logger.LogError("Rollback failed: {Error}", result.Error);
            _auditLog.Write(AuditLogEntry.RollbackFailed(AuditSource.Cli, "WFP_ERROR", result.Error.Message));
            return RollbackResponse.Failure(result.Error.Message);
        }

        _logger.LogInformation("Rollback completed, removed {FilterCount} filter(s)", result.Value);
        _auditLog.Write(AuditLogEntry.RollbackFinished(AuditSource.Cli, result.Value));
        return RollbackResponse.Success(result.Value);
    }

    private IpcResponse ProcessValidateRequest(ValidateRequest request)
    {
        _logger.LogInformation("Processing validate request");

        try
        {
            var validationResult = PolicyValidator.ValidateJson(request.PolicyJson);

            if (validationResult.IsValid)
            {
                var policy = Policy.FromJson(request.PolicyJson);
                if (policy == null)
                {
                    return ValidateResponse.Failure("Failed to parse policy after validation");
                }

                _logger.LogInformation("Policy validation succeeded: {RuleCount} rules, version {Version}",
                    policy.Rules.Count, policy.Version);
                return ValidateResponse.ForValidPolicy(policy);
            }
            else
            {
                _logger.LogInformation("Policy validation failed with {ErrorCount} error(s)",
                    validationResult.Errors.Count);
                return ValidateResponse.ForInvalidPolicy(validationResult);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during policy validation");
            return ValidateResponse.Failure($"Validation error: {ex.Message}");
        }
    }

    private IpcResponse ProcessApplyRequest(ApplyRequest request)
    {
        _logger.LogInformation("Processing apply request for policy: {PolicyPath}", request.PolicyPath);
        _auditLog.Write(AuditLogEntry.ApplyStarted(AuditSource.Cli, request.PolicyPath));

        try
        {
            // Step 1: Validate the path
            if (string.IsNullOrWhiteSpace(request.PolicyPath))
            {
                _auditLog.Write(AuditLogEntry.ApplyFailed(AuditSource.Cli, "INVALID_PATH", "Policy path is required"));
                return ApplyResponse.Failure("Policy path is required");
            }

            // Security: Check for path traversal
            if (request.PolicyPath.Contains(".."))
            {
                _logger.LogWarning("Rejected policy path with traversal: {Path}", request.PolicyPath);
                _auditLog.Write(AuditLogEntry.ApplyFailed(AuditSource.Cli, "PATH_TRAVERSAL", "Policy path cannot contain '..'"));
                return ApplyResponse.Failure("Policy path cannot contain '..' (path traversal)");
            }

            // Step 2: Read file content atomically
            // TOCTOU FIX: We perform a single atomic read using File.ReadAllBytes() instead of
            // separate File.Exists() and FileInfo checks followed by File.ReadAllText().
            // This eliminates the race window where the file could be modified between check and read.
            byte[] fileBytes;
            try
            {
                fileBytes = File.ReadAllBytes(request.PolicyPath);
            }
            catch (FileNotFoundException)
            {
                _auditLog.Write(AuditLogEntry.ApplyFailed(AuditSource.Cli, "FILE_NOT_FOUND", $"Policy file not found: {request.PolicyPath}"));
                return ApplyResponse.Failure($"Policy file not found: {request.PolicyPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read policy file: {Path}", request.PolicyPath);
                _auditLog.Write(AuditLogEntry.ApplyFailed(AuditSource.Cli, "FILE_READ_ERROR", ex.Message));
                return ApplyResponse.Failure($"Failed to read policy file: {ex.Message}");
            }

            // Step 3: Check file size (from bytes already read)
            if (fileBytes.Length > PolicyValidator.MaxPolicyFileSize)
            {
                _auditLog.Write(AuditLogEntry.ApplyFailed(AuditSource.Cli, "FILE_TOO_LARGE", "Policy file exceeds maximum size"));
                return ApplyResponse.Failure($"Policy file exceeds maximum size ({PolicyValidator.MaxPolicyFileSize / 1024} KB)");
            }

            // Step 4: Convert bytes to string
            string json = Encoding.UTF8.GetString(fileBytes);

            // Step 5: Validate the policy
            var validationResult = PolicyValidator.ValidateJson(json);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Policy validation failed with {ErrorCount} error(s)", validationResult.Errors.Count);
                _auditLog.Write(AuditLogEntry.ApplyFailed(AuditSource.Cli, "VALIDATION_FAILED", validationResult.GetSummary()));
                return ApplyResponse.Failure($"Policy validation failed: {validationResult.GetSummary()}");
            }

            // Step 6: Parse the policy
            var policy = Policy.FromJson(json);
            if (policy == null)
            {
                _auditLog.Write(AuditLogEntry.ApplyFailed(AuditSource.Cli, "PARSE_ERROR", "Failed to parse policy after validation"));
                return ApplyResponse.Failure("Failed to parse policy after validation");
            }

            _logger.LogInformation("Policy loaded: version={Version}, rules={RuleCount}",
                policy.Version, policy.Rules.Count);

            // Step 7: Compile the policy to WFP filters
            var compilationResult = RuleCompiler.Compile(policy);
            if (!compilationResult.IsSuccess)
            {
                _logger.LogWarning("Policy compilation failed with {ErrorCount} error(s)",
                    compilationResult.Errors.Count);
                _auditLog.Write(AuditLogEntry.ApplyFailed(AuditSource.Cli, "COMPILATION_FAILED", "Policy compilation failed"));
                return ApplyResponse.CompilationFailed(compilationResult);
            }

            _logger.LogInformation("Policy compiled: {FilterCount} filter(s), {SkippedCount} rule(s) skipped",
                compilationResult.Filters.Count, compilationResult.SkippedRules);

            // Step 8: Apply the compiled filters to WFP
            var applyResult = _wfpEngine.ApplyFilters(compilationResult.Filters);
            if (applyResult.IsFailure)
            {
                _logger.LogError("Failed to apply filters to WFP: {Error}", applyResult.Error);
                _auditLog.Write(AuditLogEntry.ApplyFailed(AuditSource.Cli, "WFP_ERROR", applyResult.Error.Message));
                return ApplyResponse.Failure($"Failed to apply filters: {applyResult.Error.Message}");
            }

            _logger.LogInformation("Policy applied successfully: {Created} filter(s) created, {Removed} filter(s) removed",
                applyResult.Value.FiltersCreated, applyResult.Value.FiltersRemoved);

            // Step 9: Save as LKG (Last Known Good) policy
            var lkgResult = LkgStore.Save(json, request.PolicyPath);
            if (lkgResult.IsFailure)
            {
                _logger.LogWarning("Failed to save LKG policy (non-fatal): {Error}", lkgResult.Error);
                // Don't fail the apply - LKG save failure is non-fatal
            }
            else
            {
                _logger.LogInformation("Policy saved as LKG at: {Path}", WfpConstants.GetLkgPolicyPath());
            }

            _auditLog.Write(AuditLogEntry.ApplyFinished(
                AuditSource.Cli,
                applyResult.Value.FiltersCreated,
                applyResult.Value.FiltersRemoved,
                compilationResult.SkippedRules,
                policy.Version,
                policy.Rules.Count));

            return ApplyResponse.Success(
                applyResult.Value.FiltersCreated,
                applyResult.Value.FiltersRemoved,
                compilationResult.SkippedRules,
                policy.Version,
                policy.Rules.Count,
                compilationResult.Warnings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during policy apply");
            _auditLog.Write(AuditLogEntry.ApplyFailed(AuditSource.Cli, "EXCEPTION", ex.Message));
            return ApplyResponse.Failure($"Apply error: {ex.Message}");
        }
    }

    private IpcResponse ProcessLkgShowRequest()
    {
        _logger.LogInformation("Processing LKG show request");

        try
        {
            var lkgPath = WfpConstants.GetLkgPolicyPath();
            var metadataResult = LkgStore.GetMetadata();

            if (metadataResult.IsFailure)
            {
                return LkgShowResponse.Failure(metadataResult.Error.Message);
            }

            var metadata = metadataResult.Value;

            if (!metadata.Exists)
            {
                _logger.LogInformation("No LKG policy found at: {Path}", lkgPath);
                return LkgShowResponse.NotFound(lkgPath);
            }

            if (metadata.IsCorrupt)
            {
                _logger.LogWarning("LKG policy is corrupt: {Error}", metadata.Error);
                return LkgShowResponse.Corrupt(metadata.Error ?? "Unknown error", lkgPath);
            }

            _logger.LogInformation("LKG policy found: version={Version}, rules={RuleCount}, saved={SavedAt}",
                metadata.PolicyVersion, metadata.RuleCount, metadata.SavedAt);

            return LkgShowResponse.Found(
                metadata.PolicyVersion,
                metadata.RuleCount,
                metadata.SavedAt ?? DateTime.MinValue,
                metadata.SourcePath,
                lkgPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during LKG show");
            return LkgShowResponse.Failure($"LKG show error: {ex.Message}");
        }
    }

    private IpcResponse ProcessLkgRevertRequest()
    {
        _logger.LogInformation("Processing LKG revert request");
        _auditLog.Write(AuditLogEntry.LkgRevertStarted(AuditSource.Cli));

        try
        {
            // Step 1: Load the LKG policy
            var loadResult = LkgStore.Load();

            if (!loadResult.Exists)
            {
                if (loadResult.Error != null)
                {
                    _logger.LogWarning("LKG policy is corrupt or invalid: {Error}", loadResult.Error);
                    _auditLog.Write(AuditLogEntry.LkgRevertFailed(AuditSource.Cli, "CORRUPT_LKG", loadResult.Error));
                    return LkgRevertResponse.Failure($"LKG policy is corrupt: {loadResult.Error}");
                }
                _logger.LogInformation("No LKG policy found");
                _auditLog.Write(AuditLogEntry.LkgRevertFailed(AuditSource.Cli, "NO_LKG", "No LKG policy found"));
                return LkgRevertResponse.NotFound();
            }

            var policy = loadResult.Policy!;
            var policyJson = loadResult.PolicyJson!;

            _logger.LogInformation("LKG policy loaded: version={Version}, rules={RuleCount}",
                policy.Version, policy.Rules.Count);

            // Step 2: Compile the policy to WFP filters
            var compilationResult = RuleCompiler.Compile(policy);
            if (!compilationResult.IsSuccess)
            {
                _logger.LogWarning("LKG policy compilation failed with {ErrorCount} error(s)",
                    compilationResult.Errors.Count);
                _auditLog.Write(AuditLogEntry.LkgRevertFailed(AuditSource.Cli, "COMPILATION_FAILED", "LKG policy compilation failed"));
                return LkgRevertResponse.Failure($"LKG policy compilation failed");
            }

            _logger.LogInformation("LKG policy compiled: {FilterCount} filter(s), {SkippedCount} rule(s) skipped",
                compilationResult.Filters.Count, compilationResult.SkippedRules);

            // Step 3: Apply the compiled filters to WFP
            var applyResult = _wfpEngine.ApplyFilters(compilationResult.Filters);
            if (applyResult.IsFailure)
            {
                _logger.LogError("Failed to apply LKG filters to WFP: {Error}", applyResult.Error);
                _auditLog.Write(AuditLogEntry.LkgRevertFailed(AuditSource.Cli, "WFP_ERROR", applyResult.Error.Message));
                return LkgRevertResponse.Failure($"Failed to apply LKG filters: {applyResult.Error.Message}");
            }

            _logger.LogInformation("LKG policy reverted successfully: {Created} filter(s) created, {Removed} filter(s) removed",
                applyResult.Value.FiltersCreated, applyResult.Value.FiltersRemoved);

            _auditLog.Write(AuditLogEntry.LkgRevertFinished(
                AuditSource.Cli,
                applyResult.Value.FiltersCreated,
                applyResult.Value.FiltersRemoved,
                compilationResult.SkippedRules,
                policy.Version,
                policy.Rules.Count));

            return LkgRevertResponse.Success(
                applyResult.Value.FiltersCreated,
                applyResult.Value.FiltersRemoved,
                compilationResult.SkippedRules,
                policy.Version,
                policy.Rules.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during LKG revert");
            _auditLog.Write(AuditLogEntry.LkgRevertFailed(AuditSource.Cli, "EXCEPTION", ex.Message));
            return LkgRevertResponse.Failure($"LKG revert error: {ex.Message}");
        }
    }

    private IpcResponse ProcessWatchSetRequest(WatchSetRequest request)
    {
        _logger.LogInformation("Processing watch-set request");

        try
        {
            // If no path provided, disable watching
            if (string.IsNullOrWhiteSpace(request.PolicyPath))
            {
                _fileWatcher.StopWatching();
                _logger.LogInformation("File watching disabled");
                return WatchSetResponse.Disabled();
            }

            // Get absolute path
            string absolutePath;
            try
            {
                absolutePath = Path.GetFullPath(request.PolicyPath);
            }
            catch (Exception ex)
            {
                return WatchSetResponse.Failure($"Invalid policy path: {ex.Message}");
            }

            // Start watching
            var result = _fileWatcher.StartWatching(absolutePath);
            if (result.IsFailure)
            {
                _logger.LogWarning("Failed to start file watching: {Error}", result.Error.Message);
                return WatchSetResponse.Failure(result.Error.Message);
            }

            _logger.LogInformation("File watching enabled on: {Path}", absolutePath);
            return WatchSetResponse.Success(
                watching: true,
                policyPath: absolutePath,
                initialApplySuccess: result.Value.InitialApplySuccess,
                warning: result.Value.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during watch-set");
            return WatchSetResponse.Failure($"Watch-set error: {ex.Message}");
        }
    }

    private IpcResponse ProcessWatchStatusRequest()
    {
        _logger.LogInformation("Processing watch-status request");

        try
        {
            return WatchStatusResponse.Success(
                watching: _fileWatcher.IsWatching,
                policyPath: _fileWatcher.WatchedPath,
                debounceMs: _fileWatcher.DebounceMs,
                lastApplyTime: _fileWatcher.LastApplyTime,
                lastError: _fileWatcher.LastError,
                lastErrorTime: _fileWatcher.LastErrorTime,
                applyCount: _fileWatcher.ApplyCount,
                errorCount: _fileWatcher.ErrorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during watch-status");
            return WatchStatusResponse.Failure($"Watch-status error: {ex.Message}");
        }
    }

    private IpcResponse ProcessAuditLogsRequest(AuditLogsRequest request)
    {
        _logger.LogDebug("Processing audit-logs request");

        try
        {
            List<AuditLogEntry> entries;

            // Query entries based on request parameters
            if (request.Tail > 0)
            {
                entries = _auditLogReader.ReadTail(request.Tail);
            }
            else if (request.SinceMinutes > 0)
            {
                entries = _auditLogReader.ReadSince(request.SinceMinutes);
            }
            else
            {
                // Default to last 20 entries
                entries = _auditLogReader.ReadTail(20);
            }

            var totalCount = _auditLogReader.GetEntryCount();

            // Convert to DTOs
            var dtos = entries.Select(e => new AuditLogEntryDto
            {
                Timestamp = e.Timestamp,
                Event = e.Event,
                Source = e.Source,
                Status = e.Status,
                ErrorCode = e.ErrorCode,
                ErrorMessage = e.ErrorMessage,
                PolicyFile = e.Details?.PolicyFile,
                PolicyVersion = e.Details?.PolicyVersion,
                FiltersCreated = e.Details?.FiltersCreated ?? 0,
                FiltersRemoved = e.Details?.FiltersRemoved ?? 0,
                RulesSkipped = e.Details?.RulesSkipped ?? 0,
                TotalRules = e.Details?.TotalRules ?? 0
            }).ToList();

            return AuditLogsResponse.Success(dtos, totalCount, _auditLogReader.LogPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during audit-logs request");
            return AuditLogsResponse.Failure($"Failed to read audit logs: {ex.Message}");
        }
    }

    private async Task SendResponseAsync(NamedPipeServerStream pipeServer, IpcResponse response, CancellationToken cancellationToken)
    {
        try
        {
            var json = IpcMessageParser.SerializeResponse(response);
            _logger.LogDebug("Sending response: {Response}", json);

            var messageBytes = Encoding.UTF8.GetBytes(json);
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

            // Write length prefix
            await pipeServer.WriteAsync(lengthBytes, cancellationToken);

            // Write message body
            await pipeServer.WriteAsync(messageBytes, cancellationToken);

            await pipeServer.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending response");
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
    }
}
