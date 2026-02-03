using System.Text.Json.Serialization;

namespace WfpTrafficControl.Shared.Ipc;

/// <summary>
/// Request to retrieve audit log entries.
/// Request: { "type": "audit-logs", "tail": 20 } or { "type": "audit-logs", "sinceMinutes": 60 }
/// </summary>
public sealed class AuditLogsRequest : IpcRequest
{
    public const string RequestType = "audit-logs";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// Number of recent entries to return. If specified, sinceMinutes is ignored.
    /// </summary>
    [JsonPropertyName("tail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Tail { get; set; }

    /// <summary>
    /// Return entries from the last N minutes. Only used if tail is 0.
    /// </summary>
    [JsonPropertyName("sinceMinutes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int SinceMinutes { get; set; }
}

/// <summary>
/// Response containing audit log entries.
/// </summary>
public sealed class AuditLogsResponse : IpcResponse
{
    /// <summary>
    /// List of audit log entries, newest first.
    /// </summary>
    [JsonPropertyName("entries")]
    public List<AuditLogEntryDto> Entries { get; set; } = new();

    /// <summary>
    /// Total number of entries returned.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>
    /// Total number of entries in the log file.
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /// <summary>
    /// Path to the audit log file.
    /// </summary>
    [JsonPropertyName("logPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LogPath { get; set; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static AuditLogsResponse Success(List<AuditLogEntryDto> entries, int totalCount, string logPath)
    {
        return new AuditLogsResponse
        {
            Ok = true,
            Entries = entries,
            Count = entries.Count,
            TotalCount = totalCount,
            LogPath = logPath
        };
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static AuditLogsResponse Failure(string error)
    {
        return new AuditLogsResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// DTO for audit log entries over IPC.
/// Mirrors AuditLogEntry but as a flat DTO for serialization.
/// </summary>
public sealed class AuditLogEntryDto
{
    /// <summary>
    /// Timestamp in ISO 8601 format (UTC).
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// Event type (e.g., "apply-started", "apply-finished").
    /// </summary>
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    /// <summary>
    /// Source of the operation (e.g., "cli", "hot-reload").
    /// </summary>
    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; set; }

    /// <summary>
    /// Status of the operation (e.g., "success", "failure").
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
    /// Policy file name (redacted).
    /// </summary>
    [JsonPropertyName("policyFile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyFile { get; set; }

    /// <summary>
    /// Policy version.
    /// </summary>
    [JsonPropertyName("policyVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyVersion { get; set; }

    /// <summary>
    /// Number of filters created.
    /// </summary>
    [JsonPropertyName("filtersCreated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FiltersCreated { get; set; }

    /// <summary>
    /// Number of filters removed.
    /// </summary>
    [JsonPropertyName("filtersRemoved")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int FiltersRemoved { get; set; }

    /// <summary>
    /// Number of rules skipped.
    /// </summary>
    [JsonPropertyName("rulesSkipped")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int RulesSkipped { get; set; }

    /// <summary>
    /// Total number of rules.
    /// </summary>
    [JsonPropertyName("totalRules")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TotalRules { get; set; }
}
