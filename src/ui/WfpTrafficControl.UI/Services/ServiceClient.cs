using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;

namespace WfpTrafficControl.UI.Services;

/// <summary>
/// Named pipe client for UI-to-service IPC communication.
/// Implements the same length-prefix framing protocol as the CLI.
/// </summary>
public sealed class ServiceClient : IServiceClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly object _lock = new();
    private NamedPipeClientStream? _pipe;
    private bool _disposed;

    /// <inheritdoc />
    public bool IsConnected => _pipe?.IsConnected ?? false;

    /// <inheritdoc />
    public async Task<Result<PingResponse>> PingAsync(CancellationToken ct = default)
    {
        return await SendRequestAsync<PingRequest, PingResponse>(new PingRequest(), ct);
    }

    /// <inheritdoc />
    public async Task<Result<ApplyResponse>> ApplyAsync(string policyPath, CancellationToken ct = default)
    {
        var request = new ApplyRequest { PolicyPath = policyPath };
        return await SendRequestAsync<ApplyRequest, ApplyResponse>(request, ct);
    }

    /// <inheritdoc />
    public async Task<Result<RollbackResponse>> RollbackAsync(CancellationToken ct = default)
    {
        return await SendRequestAsync<RollbackRequest, RollbackResponse>(new RollbackRequest(), ct);
    }

    /// <inheritdoc />
    public async Task<Result<LkgShowResponse>> GetLkgAsync(CancellationToken ct = default)
    {
        return await SendRequestAsync<LkgShowRequest, LkgShowResponse>(new LkgShowRequest(), ct);
    }

    /// <inheritdoc />
    public async Task<Result<LkgRevertResponse>> RevertToLkgAsync(CancellationToken ct = default)
    {
        return await SendRequestAsync<LkgRevertRequest, LkgRevertResponse>(new LkgRevertRequest(), ct);
    }

    /// <inheritdoc />
    public async Task<Result<AuditLogsResponse>> GetLogsAsync(int? tail = null, int? sinceMinutes = null, CancellationToken ct = default)
    {
        var request = new AuditLogsRequest
        {
            Tail = tail ?? 0,
            SinceMinutes = sinceMinutes ?? 0
        };
        return await SendRequestAsync<AuditLogsRequest, AuditLogsResponse>(request, ct);
    }

    /// <inheritdoc />
    public async Task<Result<ValidateResponse>> ValidateAsync(string policyJson, CancellationToken ct = default)
    {
        var request = new ValidateRequest { PolicyJson = policyJson };
        return await SendRequestAsync<ValidateRequest, ValidateResponse>(request, ct);
    }

    /// <inheritdoc />
    public async Task<Result<WatchSetResponse>> WatchSetAsync(string? policyPath, CancellationToken ct = default)
    {
        var request = new WatchSetRequest { PolicyPath = policyPath };
        return await SendRequestAsync<WatchSetRequest, WatchSetResponse>(request, ct);
    }

    /// <inheritdoc />
    public async Task<Result<WatchStatusResponse>> WatchStatusAsync(CancellationToken ct = default)
    {
        return await SendRequestAsync<WatchStatusRequest, WatchStatusResponse>(new WatchStatusRequest(), ct);
    }

    /// <inheritdoc />
    public async Task<Result<BootstrapResponse>> BootstrapAsync(CancellationToken ct = default)
    {
        return await SendRequestAsync<BootstrapRequest, BootstrapResponse>(new BootstrapRequest(), ct);
    }

    /// <inheritdoc />
    public async Task<Result<TeardownResponse>> TeardownAsync(CancellationToken ct = default)
    {
        return await SendRequestAsync<TeardownRequest, TeardownResponse>(new TeardownRequest(), ct);
    }

    /// <summary>
    /// Sends a request to the service and receives a response.
    /// Creates a new connection for each request (simple approach for UI).
    /// </summary>
    private async Task<Result<TResponse>> SendRequestAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
        where TRequest : IpcRequest
        where TResponse : IpcResponse
    {
        NamedPipeClientStream? pipe = null;

        try
        {
            // Create a new pipe connection for this request
            pipe = new NamedPipeClientStream(
                ".",
                WfpConstants.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            // Connect with timeout
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(WfpConstants.IpcConnectTimeoutMs);

            try
            {
                await pipe.ConnectAsync(connectCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return Result<TResponse>.Failure(
                    ErrorCodes.ServiceUnavailable,
                    $"Cannot connect to {WfpConstants.ServiceName} service. Is the service running?");
            }

            // Set protocol version
            request.ProtocolVersion = WfpConstants.IpcProtocolVersion;

            // Serialize request
            var json = JsonSerializer.Serialize(request, request.GetType(), JsonOptions);
            var messageBytes = Encoding.UTF8.GetBytes(json);
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

            // Create timeout for read/write
            using var opCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            opCts.CancelAfter(WfpConstants.IpcReadTimeoutMs);

            // Send length prefix + message
            await pipe.WriteAsync(lengthBytes, opCts.Token);
            await pipe.WriteAsync(messageBytes, opCts.Token);
            await pipe.FlushAsync(opCts.Token);

            // Read response length
            var responseLengthBytes = new byte[4];
            var bytesRead = await ReadExactlyAsync(pipe, responseLengthBytes, 4, opCts.Token);
            if (bytesRead < 4)
            {
                return Result<TResponse>.Failure(
                    ErrorCodes.ServiceUnavailable,
                    "Service disconnected before sending response.");
            }

            var responseLength = BitConverter.ToInt32(responseLengthBytes, 0);
            if (responseLength <= 0 || responseLength > WfpConstants.IpcMaxMessageSize)
            {
                return Result<TResponse>.Failure(
                    ErrorCodes.InvalidArgument,
                    $"Invalid response length: {responseLength}");
            }

            // Read response body
            var responseBytes = new byte[responseLength];
            bytesRead = await ReadExactlyAsync(pipe, responseBytes, responseLength, opCts.Token);
            if (bytesRead < responseLength)
            {
                return Result<TResponse>.Failure(
                    ErrorCodes.ServiceUnavailable,
                    "Service disconnected while sending response.");
            }

            var responseJson = Encoding.UTF8.GetString(responseBytes);

            // Deserialize response
            var response = JsonSerializer.Deserialize<TResponse>(responseJson, JsonOptions);
            if (response == null)
            {
                return Result<TResponse>.Failure(
                    ErrorCodes.InvalidArgument,
                    "Failed to deserialize response.");
            }

            return Result<TResponse>.Success(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Result<TResponse>.Failure(
                ErrorCodes.AccessDenied,
                "Access denied. Ensure the application is running as Administrator.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return Result<TResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Operation was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return Result<TResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Request timed out waiting for service response.");
        }
        catch (IOException ex)
        {
            return Result<TResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                $"Communication error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<TResponse>.Failure(
                ErrorCodes.InvalidArgument,
                $"Invalid JSON response: {ex.Message}");
        }
        finally
        {
            pipe?.Dispose();
        }
    }

    /// <summary>
    /// Reads exactly the specified number of bytes from the pipe.
    /// </summary>
    private static async Task<int> ReadExactlyAsync(NamedPipeClientStream pipe, byte[] buffer, int count, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await pipe.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), ct);
            if (read == 0)
            {
                break;
            }
            totalRead += read;
        }
        return totalRead;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            _pipe?.Dispose();
            _pipe = null;
        }
    }
}
