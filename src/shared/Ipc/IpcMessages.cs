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
/// Bootstrap request to ensure WFP provider and sublayer exist.
/// Request: { "type": "bootstrap" }
/// </summary>
public sealed class BootstrapRequest : IpcRequest
{
    public const string RequestType = "bootstrap";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Teardown request to remove WFP provider and sublayer (panic rollback).
/// Request: { "type": "teardown" }
/// </summary>
public sealed class TeardownRequest : IpcRequest
{
    public const string RequestType = "teardown";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Enable demo block filter request.
/// Request: { "type": "demo-block-enable" }
/// </summary>
public sealed class DemoBlockEnableRequest : IpcRequest
{
    public const string RequestType = "demo-block-enable";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Disable demo block filter request.
/// Request: { "type": "demo-block-disable" }
/// </summary>
public sealed class DemoBlockDisableRequest : IpcRequest
{
    public const string RequestType = "demo-block-disable";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Demo block filter status request.
/// Request: { "type": "demo-block-status" }
/// </summary>
public sealed class DemoBlockStatusRequest : IpcRequest
{
    public const string RequestType = "demo-block-status";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Rollback request to remove all filters but keep provider/sublayer.
/// Request: { "type": "rollback" }
/// </summary>
public sealed class RollbackRequest : IpcRequest
{
    public const string RequestType = "rollback";

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
/// Response to a bootstrap request.
/// Response: { "ok": true, "providerCreated": true/false, "sublayerCreated": true/false }
/// </summary>
public sealed class BootstrapResponse : IpcResponse
{
    /// <summary>
    /// True if the provider was created (false if it already existed).
    /// </summary>
    [JsonPropertyName("providerExists")]
    public bool ProviderExists { get; set; }

    /// <summary>
    /// True if the sublayer was created (false if it already existed).
    /// </summary>
    [JsonPropertyName("sublayerExists")]
    public bool SublayerExists { get; set; }

    /// <summary>
    /// Creates a successful bootstrap response.
    /// </summary>
    public static BootstrapResponse Success(bool providerExists, bool sublayerExists)
    {
        return new BootstrapResponse
        {
            Ok = true,
            ProviderExists = providerExists,
            SublayerExists = sublayerExists
        };
    }

    /// <summary>
    /// Creates a failed bootstrap response.
    /// </summary>
    public static BootstrapResponse Failure(string error)
    {
        return new BootstrapResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Response to a teardown request.
/// Response: { "ok": true, "providerRemoved": true/false, "sublayerRemoved": true/false }
/// </summary>
public sealed class TeardownResponse : IpcResponse
{
    /// <summary>
    /// True if the provider was removed (false if it didn't exist).
    /// </summary>
    [JsonPropertyName("providerRemoved")]
    public bool ProviderRemoved { get; set; }

    /// <summary>
    /// True if the sublayer was removed (false if it didn't exist).
    /// </summary>
    [JsonPropertyName("sublayerRemoved")]
    public bool SublayerRemoved { get; set; }

    /// <summary>
    /// Creates a successful teardown response.
    /// </summary>
    public static TeardownResponse Success(bool providerRemoved, bool sublayerRemoved)
    {
        return new TeardownResponse
        {
            Ok = true,
            ProviderRemoved = providerRemoved,
            SublayerRemoved = sublayerRemoved
        };
    }

    /// <summary>
    /// Creates a failed teardown response.
    /// </summary>
    public static TeardownResponse Failure(string error)
    {
        return new TeardownResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Response to a demo block enable request.
/// Response: { "ok": true, "filterEnabled": true }
/// </summary>
public sealed class DemoBlockEnableResponse : IpcResponse
{
    /// <summary>
    /// True if the filter is now active.
    /// </summary>
    [JsonPropertyName("filterEnabled")]
    public bool FilterEnabled { get; set; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static DemoBlockEnableResponse Success(bool filterEnabled)
    {
        return new DemoBlockEnableResponse
        {
            Ok = true,
            FilterEnabled = filterEnabled
        };
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static DemoBlockEnableResponse Failure(string error)
    {
        return new DemoBlockEnableResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Response to a demo block disable request.
/// Response: { "ok": true, "filterDisabled": true }
/// </summary>
public sealed class DemoBlockDisableResponse : IpcResponse
{
    /// <summary>
    /// True if the filter was removed (or didn't exist).
    /// </summary>
    [JsonPropertyName("filterDisabled")]
    public bool FilterDisabled { get; set; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static DemoBlockDisableResponse Success(bool filterDisabled)
    {
        return new DemoBlockDisableResponse
        {
            Ok = true,
            FilterDisabled = filterDisabled
        };
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static DemoBlockDisableResponse Failure(string error)
    {
        return new DemoBlockDisableResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Response to a demo block status request.
/// Response: { "ok": true, "filterActive": true, "blockedTarget": "1.1.1.1:443" }
/// </summary>
public sealed class DemoBlockStatusResponse : IpcResponse
{
    /// <summary>
    /// True if the demo block filter is currently active.
    /// </summary>
    [JsonPropertyName("filterActive")]
    public bool FilterActive { get; set; }

    /// <summary>
    /// Description of what is blocked (for display).
    /// </summary>
    [JsonPropertyName("blockedTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BlockedTarget { get; set; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static DemoBlockStatusResponse Success(bool filterActive)
    {
        return new DemoBlockStatusResponse
        {
            Ok = true,
            FilterActive = filterActive,
            BlockedTarget = filterActive ? "TCP 1.1.1.1:443 (Cloudflare)" : null
        };
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static DemoBlockStatusResponse Failure(string error)
    {
        return new DemoBlockStatusResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Response to a rollback request.
/// Response: { "ok": true, "filtersRemoved": 5 }
/// </summary>
public sealed class RollbackResponse : IpcResponse
{
    /// <summary>
    /// Number of filters that were removed.
    /// </summary>
    [JsonPropertyName("filtersRemoved")]
    public int FiltersRemoved { get; set; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static RollbackResponse Success(int filtersRemoved)
    {
        return new RollbackResponse
        {
            Ok = true,
            FiltersRemoved = filtersRemoved
        };
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static RollbackResponse Failure(string error)
    {
        return new RollbackResponse
        {
            Ok = false,
            Error = error
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
                BootstrapRequest.RequestType => Result<IpcRequest>.Success(new BootstrapRequest()),
                TeardownRequest.RequestType => Result<IpcRequest>.Success(new TeardownRequest()),
                DemoBlockEnableRequest.RequestType => Result<IpcRequest>.Success(new DemoBlockEnableRequest()),
                DemoBlockDisableRequest.RequestType => Result<IpcRequest>.Success(new DemoBlockDisableRequest()),
                DemoBlockStatusRequest.RequestType => Result<IpcRequest>.Success(new DemoBlockStatusRequest()),
                RollbackRequest.RequestType => Result<IpcRequest>.Success(new RollbackRequest()),
                ValidateRequest.RequestType => ParseValidateRequest(json),
                ApplyRequest.RequestType => ParseApplyRequest(json),
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

    /// <summary>
    /// Parses a validate request, extracting the policy JSON.
    /// </summary>
    private static Result<IpcRequest> ParseValidateRequest(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<ValidateRequest>(json, JsonOptions);
            if (request == null)
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Failed to parse validate request.");
            }
            if (string.IsNullOrWhiteSpace(request.PolicyJson))
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Missing 'policyJson' field in validate request.");
            }
            return Result<IpcRequest>.Success(request);
        }
        catch (JsonException ex)
        {
            return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, $"Invalid validate request JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses an apply request, extracting the policy path.
    /// </summary>
    private static Result<IpcRequest> ParseApplyRequest(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<ApplyRequest>(json, JsonOptions);
            if (request == null)
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Failed to parse apply request.");
            }
            if (string.IsNullOrWhiteSpace(request.PolicyPath))
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Missing 'policyPath' field in apply request.");
            }
            return Result<IpcRequest>.Success(request);
        }
        catch (JsonException ex)
        {
            return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, $"Invalid apply request JSON: {ex.Message}");
        }
    }
}
