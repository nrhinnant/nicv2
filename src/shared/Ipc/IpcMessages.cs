using System.Text.Json;
using System.Text.Json.Serialization;

namespace WfpTrafficControl.Shared.Ipc;

/// <summary>
/// Base class for all IPC requests.
/// All requests must have a "type" field to identify the request type.
/// </summary>
public abstract class IpcRequest
{
    /// <summary>
    /// The type of the request (e.g., "ping").
    /// </summary>
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

/// <summary>
/// Ping request to check if the service is alive and get basic info.
/// Request: { "type": "ping" }
/// </summary>
public sealed class PingRequest : IpcRequest
{
    public const string RequestType = "ping";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Base class for all IPC responses.
/// </summary>
public abstract class IpcResponse
{
    /// <summary>
    /// True if the request was processed successfully.
    /// </summary>
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    /// <summary>
    /// Error message if Ok is false.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; set; }
}

/// <summary>
/// Response to a ping request.
/// Response: { "ok": true, "serviceVersion": "...", "time": "..." }
/// </summary>
public sealed class PingResponse : IpcResponse
{
    /// <summary>
    /// The version of the service.
    /// </summary>
    [JsonPropertyName("serviceVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceVersion { get; set; }

    /// <summary>
    /// The current time on the service (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Time { get; set; }

    /// <summary>
    /// Creates a successful ping response.
    /// </summary>
    public static PingResponse Success(string serviceVersion)
    {
        return new PingResponse
        {
            Ok = true,
            ServiceVersion = serviceVersion,
            Time = DateTimeOffset.UtcNow.ToString("o")
        };
    }
}

/// <summary>
/// Generic error response for any failed request.
/// </summary>
public sealed class ErrorResponse : IpcResponse
{
    public ErrorResponse()
    {
        Ok = false;
    }

    public ErrorResponse(string error)
    {
        Ok = false;
        Error = error;
    }

    /// <summary>
    /// Creates an error response for access denied.
    /// </summary>
    public static ErrorResponse AccessDenied() => new("Access denied. Administrator privileges required.");

    /// <summary>
    /// Creates an error response for invalid JSON.
    /// </summary>
    public static ErrorResponse InvalidJson(string details) => new($"Invalid JSON: {details}");

    /// <summary>
    /// Creates an error response for unknown request type.
    /// </summary>
    public static ErrorResponse UnknownRequestType(string type) => new($"Unknown request type: {type}");

    /// <summary>
    /// Creates an error response for missing request type.
    /// </summary>
    public static ErrorResponse MissingRequestType() => new("Missing or invalid 'type' field in request.");
}

/// <summary>
/// Handles parsing and validation of IPC messages.
/// </summary>
public static class IpcMessageParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>
    /// Parses a JSON string into an IpcRequest.
    /// Returns the parsed request or an error.
    /// </summary>
    public static Result<IpcRequest> ParseRequest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Request cannot be empty.");
        }

        try
        {
            // First, parse as a generic JSON document to extract the type
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Request must be a JSON object.");
            }

            if (!root.TryGetProperty("type", out var typeElement))
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Missing 'type' field in request.");
            }

            if (typeElement.ValueKind != JsonValueKind.String)
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "'type' field must be a string.");
            }

            var requestType = typeElement.GetString();

            return requestType switch
            {
                PingRequest.RequestType => Result<IpcRequest>.Success(new PingRequest()),
                null => Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "'type' field cannot be null."),
                _ => Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, $"Unknown request type: {requestType}")
            };
        }
        catch (JsonException ex)
        {
            return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, $"Invalid JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Serializes an IpcResponse to JSON.
    /// </summary>
    public static string SerializeResponse(IpcResponse response)
    {
        return JsonSerializer.Serialize(response, response.GetType(), JsonOptions);
    }

    /// <summary>
    /// Creates an error response JSON string.
    /// </summary>
    public static string CreateErrorResponse(string error)
    {
        return SerializeResponse(new ErrorResponse(error));
    }
}
