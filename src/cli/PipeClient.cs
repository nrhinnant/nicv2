using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Ipc;

namespace WfpTrafficControl.Cli;

/// <summary>
/// Named pipe client for CLI-to-service IPC communication.
/// Implements the same length-prefix framing protocol as PipeServer.
/// </summary>
public sealed class PipeClient : IDisposable
{

    private readonly NamedPipeClientStream _pipeClient;
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public PipeClient()
    {
        _pipeClient = new NamedPipeClientStream(
            ".",
            WfpConstants.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
    }

    /// <summary>
    /// Connects to the service pipe.
    /// </summary>
    /// <returns>A Result indicating success or failure with error details.</returns>
    public Result<bool> Connect()
    {
        try
        {
            _pipeClient.Connect(WfpConstants.IpcConnectTimeoutMs);
            return Result<bool>.Success(true);
        }
        catch (TimeoutException)
        {
            return Result<bool>.Failure(
                ErrorCodes.ServiceUnavailable,
                $"Cannot connect to {WfpConstants.ServiceName} service. Is the service running?");
        }
        catch (UnauthorizedAccessException)
        {
            return Result<bool>.Failure(
                ErrorCodes.AccessDenied,
                "Access denied. Run the CLI as Administrator.");
        }
        catch (IOException ex)
        {
            return Result<bool>.Failure(
                ErrorCodes.ServiceUnavailable,
                $"Cannot connect to service: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a request to the service and receives a response.
    /// </summary>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <returns>A Result containing the response or an error.</returns>
    public Result<TResponse> SendRequest<TResponse>(IpcRequest request) where TResponse : IpcResponse
    {
        if (!_pipeClient.IsConnected)
        {
            return Result<TResponse>.Failure(ErrorCodes.InvalidState, "Not connected to service.");
        }

        try
        {
            // Use async internally with a timeout
            return SendRequestAsync<TResponse>(request).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return Result<TResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Request timed out waiting for service response.");
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
            return Result<TResponse>.Failure(
                ErrorCodes.ServiceUnavailable,
                "Request timed out waiting for service response.");
        }
    }

    private async Task<Result<TResponse>> SendRequestAsync<TResponse>(IpcRequest request) where TResponse : IpcResponse
    {
        using var cts = new CancellationTokenSource(WfpConstants.IpcReadTimeoutMs);
        var cancellationToken = cts.Token;

        try
        {
            // Set protocol version before serializing
            request.ProtocolVersion = WfpConstants.IpcProtocolVersion;

            // Serialize the request
            var json = JsonSerializer.Serialize(request, request.GetType(), JsonOptions);
            var messageBytes = Encoding.UTF8.GetBytes(json);
            var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

            // Send length prefix + message
            await _pipeClient.WriteAsync(lengthBytes, cancellationToken);
            await _pipeClient.WriteAsync(messageBytes, cancellationToken);
            await _pipeClient.FlushAsync(cancellationToken);

            // Read response length
            var responseLengthBytes = new byte[4];
            var bytesRead = await ReadExactlyAsync(responseLengthBytes, 4, cancellationToken);
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
            bytesRead = await ReadExactlyAsync(responseBytes, responseLength, cancellationToken);
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
    }

    /// <summary>
    /// Reads exactly the specified number of bytes from the pipe.
    /// </summary>
    private async Task<int> ReadExactlyAsync(byte[] buffer, int count, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await _pipeClient.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), cancellationToken);
            if (read == 0)
            {
                // End of stream
                break;
            }
            totalRead += read;
        }
        return totalRead;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pipeClient.Dispose();
    }
}

/// <summary>
/// Helper class for serializing CLI requests for testing purposes.
/// </summary>
public static class CliRequestSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>
    /// Serializes an IPC request to JSON.
    /// </summary>
    public static string Serialize(IpcRequest request)
    {
        return JsonSerializer.Serialize(request, request.GetType(), JsonOptions);
    }

    /// <summary>
    /// Serializes an IPC request to the wire format (length prefix + JSON).
    /// </summary>
    public static byte[] SerializeToWireFormat(IpcRequest request)
    {
        var json = Serialize(request);
        var messageBytes = Encoding.UTF8.GetBytes(json);
        var lengthBytes = BitConverter.GetBytes(messageBytes.Length);

        var result = new byte[lengthBytes.Length + messageBytes.Length];
        Buffer.BlockCopy(lengthBytes, 0, result, 0, lengthBytes.Length);
        Buffer.BlockCopy(messageBytes, 0, result, lengthBytes.Length, messageBytes.Length);

        return result;
    }
}
