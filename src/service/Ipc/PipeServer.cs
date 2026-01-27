using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Logging;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;

namespace WfpTrafficControl.Service.Ipc;

/// <summary>
/// Named pipe server for CLI-to-service IPC communication.
/// Handles authorization, message parsing, and request dispatch.
/// </summary>
public sealed class PipeServer : IDisposable
{
    private readonly ILogger<PipeServer> _logger;
    private readonly string _serviceVersion;
    private readonly CancellationTokenSource _cts;
    private Task? _listenerTask;
    private bool _disposed;

    /// <summary>
    /// Maximum message size in bytes (64 KB).
    /// </summary>
    private const int MaxMessageSize = 64 * 1024;

    /// <summary>
    /// Timeout for reading from the pipe in milliseconds.
    /// </summary>
    private const int ReadTimeoutMs = 30_000;

    /// <summary>
    /// Well-known SID for LocalSystem account.
    /// </summary>
    private static readonly SecurityIdentifier LocalSystemSid = new(WellKnownSidType.LocalSystemSid, null);

    /// <summary>
    /// Well-known SID for Administrators group.
    /// </summary>
    private static readonly SecurityIdentifier AdministratorsSid = new(WellKnownSidType.BuiltinAdministratorsSid, null);

    public PipeServer(ILogger<PipeServer> logger, string serviceVersion)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceVersion = serviceVersion ?? throw new ArgumentNullException(nameof(serviceVersion));
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
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? pipeServer = null;

            try
            {
                // Create a new pipe server for each connection
                // Using PipeAccessRights to require ReadWrite access
                pipeServer = new NamedPipeServerStream(
                    WfpConstants.PipeName,
                    PipeDirection.InOut,
                    1, // maxNumberOfServerInstances - one at a time (sequential)
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

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

            // Step 3: Process the request
            var response = ProcessRequest(request);

            // Step 4: Send the response
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
            timeoutCts.CancelAfter(ReadTimeoutMs);

            // Read length prefix (4 bytes, little-endian)
            var lengthBuffer = new byte[4];
            var bytesRead = await ReadExactlyAsync(pipeServer, lengthBuffer, 0, 4, timeoutCts.Token);
            if (bytesRead < 4)
            {
                _logger.LogWarning("Client disconnected before sending complete request length");
                return null;
            }

            var messageLength = BitConverter.ToInt32(lengthBuffer, 0);

            // Validate message length
            if (messageLength <= 0 || messageLength > MaxMessageSize)
            {
                _logger.LogWarning("Invalid message length: {MessageLength}", messageLength);
                await SendResponseAsync(pipeServer, ErrorResponse.InvalidJson($"Invalid message length: {messageLength}"), cancellationToken);
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
            _ => new ErrorResponse($"Unknown request type: {request.Type}")
        };
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
