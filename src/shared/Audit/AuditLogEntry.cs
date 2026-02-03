using System.Text.Json;
using System.Text.Json.Serialization;

namespace WfpTrafficControl.Shared.Audit;

/// <summary>
/// Represents a single audit log entry for control-plane events.
/// Serialized as a single JSON line in the audit log file.
/// </summary>
public sealed class AuditLogEntry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Timestamp in ISO 8601 format (UTC).
    /// </summary>
    [JsonPropertyName("ts")]
    public string Timestamp { get; set; } = DateTimeOffset.UtcNow.ToString("o");

    /// <summary>
    /// Event type (e.g., "apply-started", "apply-finished", "rollback-started").
    /// </summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    /// <summary>
    /// Source of the operation (e.g., "cli", "hot-reload", "lkg-revert", "startup").
    /// </summary>
    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; set; }

    /// <summary>
    /// Status of the operation (e.g., "success", "failure").
    /// Only present for *-finished events.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; set; }

    /// <summary>
    /// Error code for failed operations.
    /// </summary>
    [JsonPropertyName("errorCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Error message for failed operations.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional details about the event.
    /// </summary>
    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AuditLogDetails? Details { get; set; }

    /// <summary>
    /// Serializes this entry to a JSON string (single line).
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>
    /// Parses a JSON line into an AuditLogEntry.
    /// Returns null if parsing fails.
    /// </summary>
    public static AuditLogEntry? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AuditLogEntry>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    // ========================================
    // Factory Methods for Common Events
    // ========================================

    /// <summary>
    /// Creates an apply-started event.
    /// </summary>
    public static AuditLogEntry ApplyStarted(string source, string? policyFile = null)
    {
        return new AuditLogEntry
        {
            Event = AuditEventTypes.ApplyStarted,
            Source = source,
            Details = policyFile != null ? new AuditLogDetails { PolicyFile = RedactPath(policyFile) } : null
        };
    }

    /// <summary>
    /// Creates a successful apply-finished event.
    /// </summary>
    public static AuditLogEntry ApplyFinished(string source, int filtersCreated, int filtersRemoved,
        int rulesSkipped, string? policyVersion, int totalRules)
    {
        return new AuditLogEntry
        {
            Event = AuditEventTypes.ApplyFinished,
            Source = source,
            Status = AuditStatus.Success,
            Details = new AuditLogDetails
            {
                FiltersCreated = filtersCreated,
                FiltersRemoved = filtersRemoved,
                RulesSkipped = rulesSkipped,
                PolicyVersion = policyVersion,
                TotalRules = totalRules
            }
        };
    }

    /// <summary>
    /// Creates a failed apply-finished event.
    /// </summary>
    public static AuditLogEntry ApplyFailed(string source, string errorCode, string errorMessage)
    {
        return new AuditLogEntry
        {
            Event = AuditEventTypes.ApplyFinished,
            Source = source,
            Status = AuditStatus.Failure,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Creates a rollback-started event.
    /// </summary>
    public static AuditLogEntry RollbackStarted(string source)
    {
        return new AuditLogEntry
        {
            Event = AuditEventTypes.RollbackStarted,
            Source = source
        };
    }

    /// <summary>
    /// Creates a successful rollback-finished event.
    /// </summary>
    public static AuditLogEntry RollbackFinished(string source, int filtersRemoved)
    {
        return new AuditLogEntry
        {
            Event = AuditEventTypes.RollbackFinished,
            Source = source,
            Status = AuditStatus.Success,
            Details = new AuditLogDetails { FiltersRemoved = filtersRemoved }
        };
    }

    /// <summary>
    /// Creates a failed rollback-finished event.
    /// </summary>
    public static AuditLogEntry RollbackFailed(string source, string errorCode, string errorMessage)
    {
        return new AuditLogEntry
        {
            Event = AuditEventTypes.RollbackFinished,
            Source = source,
            Status = AuditStatus.Failure,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Creates a teardown-started event.
    /// </summary>
    public static AuditLogEntry TeardownStarted(string source)
    {
        return new AuditLogEntry
        {
            Event = AuditEventTypes.TeardownStarted,
            Source = source
        };
    }

    /// <summary>
    /// Creates a successful teardown-finished event.
    /// </summary>
    public static AuditLogEntry TeardownFinished(string source, bool providerRemoved, bool sublayerRemoved)
    {
        return new AuditLogEntry
        {
            Event = AuditEventTypes.TeardownFinished,
            Source = source,
            Status = AuditStatus.Success,
            Details = new AuditLogDetails
            {
                ProviderRemoved = providerRemoved,
                SublayerRemoved = sublayerRemoved
            }
        };
    }

    /// <summary>
    /// Creates a failed teardown-finished event.
    /// </summary>
    public static AuditLogEntry TeardownFailed(string source, string errorCode, string errorMessage)
    {
        return new AuditLogEntry
        {
            Event = AuditEventTypes.TeardownFinished,
            Source = source,
            Status = AuditStatus.Failure,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Creates an lkg-revert-started event.
    /// </summary>
    public static AuditLogEntry LkgRevertStarted(string source)
    {
        return new AuditLogEntry
        {
            Event = AuditEventTypes.LkgRevertStarted,
            Source = source
        };
    }

    /// <summary>
    /// Creates a successful lkg-revert-finished event.
    /// </summary>
    public static AuditLogEntry LkgRevertFinished(string source, int filtersCreated, int filtersRemoved,
        int rulesSkipped, string? policyVersion, int totalRules)
    {
        return new AuditLogEntry
        {
            Event = AuditEventTypes.LkgRevertFinished,
            Source = source,
            Status = AuditStatus.Success,
            Details = new AuditLogDetails
            {
                FiltersCreated = filtersCreated,
                FiltersRemoved = filtersRemoved,
                RulesSkipped = rulesSkipped,
                PolicyVersion = policyVersion,
                TotalRules = totalRules
            }
        };
    }

    /// <summary>
    /// Creates a failed lkg-revert-finished event.
    /// </summary>
    public static AuditLogEntry LkgRevertFailed(string source, string errorCode, string errorMessage)
    {
        return new AuditLogEntry
        {
            Event = AuditEventTypes.LkgRevertFinished,
            Source = source,
            Status = AuditStatus.Failure,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Redacts a full file path to just the filename for security.
    /// </summary>
    private static string RedactPath(string fullPath)
    {
        try
        {
            return Path.GetFileName(fullPath);
        }
        catch
        {
            return "[redacted]";
        }
    }
}

/// <summary>
/// Additional details for audit log events.
/// </summary>
public sealed class AuditLogDetails
{
    [JsonPropertyName("policyFile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyFile { get; set; }

    [JsonPropertyName("policyVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyVersion { get; set; }

    [JsonPropertyName("totalRules")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TotalRules { get; set; }

    [JsonPropertyName("filtersCreated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FiltersCreated { get; set; }

    [JsonPropertyName("filtersRemoved")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FiltersRemoved { get; set; }

    [JsonPropertyName("rulesSkipped")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int RulesSkipped { get; set; }

    [JsonPropertyName("providerRemoved")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ProviderRemoved { get; set; }

    [JsonPropertyName("sublayerRemoved")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SublayerRemoved { get; set; }
}

/// <summary>
/// Constants for audit event types.
/// </summary>
public static class AuditEventTypes
{
    public const string ApplyStarted = "apply-started";
    public const string ApplyFinished = "apply-finished";
    public const string RollbackStarted = "rollback-started";
    public const string RollbackFinished = "rollback-finished";
    public const string TeardownStarted = "teardown-started";
    public const string TeardownFinished = "teardown-finished";
    public const string LkgRevertStarted = "lkg-revert-started";
    public const string LkgRevertFinished = "lkg-revert-finished";
}

/// <summary>
/// Constants for audit status values.
/// </summary>
public static class AuditStatus
{
    public const string Success = "success";
    public const string Failure = "failure";
}

/// <summary>
/// Constants for audit source values.
/// </summary>
public static class AuditSource
{
    public const string Cli = "cli";
    public const string HotReload = "hot-reload";
    public const string Startup = "startup";
}
