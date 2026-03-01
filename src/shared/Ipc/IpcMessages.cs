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

    /// <summary>
    /// The protocol version of the client.
    /// Optional for backward compatibility, but strongly recommended.
    /// Server will validate this against supported versions.
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ProtocolVersion { get; set; }
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
/// LKG show request to display the stored LKG policy.
/// Request: { "type": "lkg-show" }
/// </summary>
public sealed class LkgShowRequest : IpcRequest
{
    public const string RequestType = "lkg-show";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// LKG revert request to apply the stored LKG policy.
/// Request: { "type": "lkg-revert" }
/// </summary>
public sealed class LkgRevertRequest : IpcRequest
{
    public const string RequestType = "lkg-revert";

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

    /// <summary>
    /// The protocol version of the server.
    /// Always included in responses so clients can detect version mismatches.
    /// </summary>
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = WfpConstants.IpcProtocolVersion;
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
/// Response to an LKG show request.
/// Response: { "ok": true, "exists": true, "policyVersion": "1.0.0", ... }
/// </summary>
public sealed class LkgShowResponse : IpcResponse
{
    /// <summary>
    /// Whether an LKG policy exists.
    /// </summary>
    [JsonPropertyName("exists")]
    public bool Exists { get; set; }

    /// <summary>
    /// Whether the LKG file is corrupt.
    /// </summary>
    [JsonPropertyName("isCorrupt")]
    public bool IsCorrupt { get; set; }

    /// <summary>
    /// Policy version from the LKG.
    /// </summary>
    [JsonPropertyName("policyVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyVersion { get; set; }

    /// <summary>
    /// Number of rules in the LKG policy.
    /// </summary>
    [JsonPropertyName("ruleCount")]
    public int RuleCount { get; set; }

    /// <summary>
    /// When the LKG was saved.
    /// </summary>
    [JsonPropertyName("savedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SavedAt { get; set; }

    /// <summary>
    /// Path to the original policy file.
    /// </summary>
    [JsonPropertyName("sourcePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourcePath { get; set; }

    /// <summary>
    /// Path where the LKG file is stored.
    /// </summary>
    [JsonPropertyName("lkgPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LkgPath { get; set; }

    /// <summary>
    /// Creates a response indicating no LKG exists.
    /// </summary>
    public static LkgShowResponse NotFound(string lkgPath)
    {
        return new LkgShowResponse
        {
            Ok = true,
            Exists = false,
            LkgPath = lkgPath
        };
    }

    /// <summary>
    /// Creates a response for a valid LKG.
    /// </summary>
    public static LkgShowResponse Found(string? policyVersion, int ruleCount, DateTime savedAt, string? sourcePath, string lkgPath)
    {
        return new LkgShowResponse
        {
            Ok = true,
            Exists = true,
            PolicyVersion = policyVersion,
            RuleCount = ruleCount,
            SavedAt = savedAt.ToString("o"),
            SourcePath = sourcePath,
            LkgPath = lkgPath
        };
    }

    /// <summary>
    /// Creates a response for a corrupt LKG.
    /// </summary>
    public static LkgShowResponse Corrupt(string error, string lkgPath)
    {
        return new LkgShowResponse
        {
            Ok = true,
            Exists = true,
            IsCorrupt = true,
            Error = error,
            LkgPath = lkgPath
        };
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static LkgShowResponse Failure(string error)
    {
        return new LkgShowResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Response to an LKG revert request.
/// Response: { "ok": true, "filtersCreated": 5, ... }
/// </summary>
public sealed class LkgRevertResponse : IpcResponse
{
    /// <summary>
    /// Whether an LKG policy was found.
    /// </summary>
    [JsonPropertyName("lkgFound")]
    public bool LkgFound { get; set; }

    /// <summary>
    /// Policy version from the applied LKG.
    /// </summary>
    [JsonPropertyName("policyVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyVersion { get; set; }

    /// <summary>
    /// Number of rules in the LKG policy.
    /// </summary>
    [JsonPropertyName("totalRules")]
    public int TotalRules { get; set; }

    /// <summary>
    /// Number of filters created during apply.
    /// </summary>
    [JsonPropertyName("filtersCreated")]
    public int FiltersCreated { get; set; }

    /// <summary>
    /// Number of filters removed during apply.
    /// </summary>
    [JsonPropertyName("filtersRemoved")]
    public int FiltersRemoved { get; set; }

    /// <summary>
    /// Number of rules skipped (e.g., inbound rules when only outbound is supported).
    /// </summary>
    [JsonPropertyName("rulesSkipped")]
    public int RulesSkipped { get; set; }

    /// <summary>
    /// Creates a successful revert response.
    /// </summary>
    public static LkgRevertResponse Success(int filtersCreated, int filtersRemoved, int rulesSkipped, string? policyVersion, int totalRules)
    {
        return new LkgRevertResponse
        {
            Ok = true,
            LkgFound = true,
            PolicyVersion = policyVersion,
            TotalRules = totalRules,
            FiltersCreated = filtersCreated,
            FiltersRemoved = filtersRemoved,
            RulesSkipped = rulesSkipped
        };
    }

    /// <summary>
    /// Creates a response indicating no LKG exists.
    /// </summary>
    public static LkgRevertResponse NotFound()
    {
        return new LkgRevertResponse
        {
            Ok = false,
            LkgFound = false,
            Error = "No LKG policy found"
        };
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static LkgRevertResponse Failure(string error)
    {
        return new LkgRevertResponse
        {
            Ok = false,
            LkgFound = true,
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

    /// <summary>
    /// Creates an error response for protocol version mismatch.
    /// </summary>
    public static ErrorResponse ProtocolVersionMismatch(int clientVersion, int minVersion, int maxVersion) =>
        new($"Protocol version mismatch. Client version: {clientVersion}, supported range: {minVersion}-{maxVersion}. Please update the CLI.");

    /// <summary>
    /// Creates an error response for request too large.
    /// </summary>
    public static ErrorResponse RequestTooLarge(int size, int maxSize) =>
        new($"Request too large: {size} bytes exceeds maximum of {maxSize} bytes.");

    /// <summary>
    /// Creates an error response for request timeout.
    /// </summary>
    public static ErrorResponse RequestTimeout() => new("Request timed out.");
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
            // First, parse as a generic JSON document to extract the type and protocol version
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

            // Extract protocol version (optional, defaults to 0 for backward compatibility)
            int protocolVersion = 0;
            if (root.TryGetProperty("protocolVersion", out var versionElement) &&
                versionElement.ValueKind == JsonValueKind.Number)
            {
                protocolVersion = versionElement.GetInt32();
            }

            var result = requestType switch
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
                LkgShowRequest.RequestType => Result<IpcRequest>.Success(new LkgShowRequest()),
                LkgRevertRequest.RequestType => Result<IpcRequest>.Success(new LkgRevertRequest()),
                WatchSetRequest.RequestType => ParseWatchSetRequest(json),
                WatchStatusRequest.RequestType => Result<IpcRequest>.Success(new WatchStatusRequest()),
                AuditLogsRequest.RequestType => ParseAuditLogsRequest(json),
                BlockRulesRequest.RequestType => Result<IpcRequest>.Success(new BlockRulesRequest()),
                SimulateRequest.RequestType => ParseSimulateRequest(json),
                PolicyHistoryRequest.RequestType => ParsePolicyHistoryRequest(json),
                PolicyHistoryRevertRequest.RequestType => ParsePolicyHistoryRevertRequest(json),
                PolicyHistoryGetRequest.RequestType => ParsePolicyHistoryGetRequest(json),
                null => Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "'type' field cannot be null."),
                _ => Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, $"Unknown request type: {requestType}")
            };

            // Set protocol version on successfully parsed requests
            if (result.IsSuccess && result.Value != null)
            {
                result.Value.ProtocolVersion = protocolVersion;
            }

            return result;
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

    /// <summary>
    /// Parses a watch-set request, extracting the policy path.
    /// </summary>
    private static Result<IpcRequest> ParseWatchSetRequest(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<WatchSetRequest>(json, JsonOptions);
            if (request == null)
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Failed to parse watch-set request.");
            }
            // Note: PolicyPath can be null/empty to disable watching
            return Result<IpcRequest>.Success(request);
        }
        catch (JsonException ex)
        {
            return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, $"Invalid watch-set request JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses an audit-logs request, extracting the query parameters.
    /// </summary>
    private static Result<IpcRequest> ParseAuditLogsRequest(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<AuditLogsRequest>(json, JsonOptions);
            if (request == null)
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Failed to parse audit-logs request.");
            }
            return Result<IpcRequest>.Success(request);
        }
        catch (JsonException ex)
        {
            return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, $"Invalid audit-logs request JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a simulate request, extracting the connection parameters.
    /// </summary>
    private static Result<IpcRequest> ParseSimulateRequest(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<SimulateRequest>(json, JsonOptions);
            if (request == null)
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Failed to parse simulate request.");
            }
            return Result<IpcRequest>.Success(request);
        }
        catch (JsonException ex)
        {
            return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, $"Invalid simulate request JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates the protocol version of a request.
    /// Returns null if valid, or an error response if invalid.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <returns>An ErrorResponse if version is incompatible, null otherwise.</returns>
    public static ErrorResponse? ValidateProtocolVersion(IpcRequest request)
    {
        // Version 0 means client didn't send a version (backward compatibility)
        // Log a warning but allow for now
        if (request.ProtocolVersion == 0)
        {
            return null; // Allow for backward compatibility
        }

        // Check if version is within supported range
        if (request.ProtocolVersion < WfpConstants.IpcMinProtocolVersion ||
            request.ProtocolVersion > WfpConstants.IpcProtocolVersion)
        {
            return ErrorResponse.ProtocolVersionMismatch(
                request.ProtocolVersion,
                WfpConstants.IpcMinProtocolVersion,
                WfpConstants.IpcProtocolVersion);
        }

        return null;
    }

    /// <summary>
    /// Validates message size against the maximum allowed size.
    /// Returns null if valid, or an error response if too large.
    /// </summary>
    /// <param name="size">The size of the message in bytes.</param>
    /// <returns>An ErrorResponse if too large, null otherwise.</returns>
    public static ErrorResponse? ValidateMessageSize(int size)
    {
        if (size <= 0 || size > WfpConstants.IpcMaxMessageSize)
        {
            return ErrorResponse.RequestTooLarge(size, WfpConstants.IpcMaxMessageSize);
        }
        return null;
    }

    /// <summary>
    /// Parses a policy-history request, extracting the limit.
    /// </summary>
    private static Result<IpcRequest> ParsePolicyHistoryRequest(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<PolicyHistoryRequest>(json, JsonOptions);
            if (request == null)
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Failed to parse policy-history request.");
            }
            return Result<IpcRequest>.Success(request);
        }
        catch (JsonException ex)
        {
            return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, $"Invalid policy-history request JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a policy-history-revert request, extracting the entry ID.
    /// </summary>
    private static Result<IpcRequest> ParsePolicyHistoryRevertRequest(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<PolicyHistoryRevertRequest>(json, JsonOptions);
            if (request == null)
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Failed to parse policy-history-revert request.");
            }
            if (string.IsNullOrWhiteSpace(request.EntryId))
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Missing 'entryId' field in policy-history-revert request.");
            }
            return Result<IpcRequest>.Success(request);
        }
        catch (JsonException ex)
        {
            return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, $"Invalid policy-history-revert request JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a policy-history-get request, extracting the entry ID.
    /// </summary>
    private static Result<IpcRequest> ParsePolicyHistoryGetRequest(string json)
    {
        try
        {
            var request = JsonSerializer.Deserialize<PolicyHistoryGetRequest>(json, JsonOptions);
            if (request == null)
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Failed to parse policy-history-get request.");
            }
            if (string.IsNullOrWhiteSpace(request.EntryId))
            {
                return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, "Missing 'entryId' field in policy-history-get request.");
            }
            return Result<IpcRequest>.Success(request);
        }
        catch (JsonException ex)
        {
            return Result<IpcRequest>.Failure(ErrorCodes.InvalidArgument, $"Invalid policy-history-get request JSON: {ex.Message}");
        }
    }
}
