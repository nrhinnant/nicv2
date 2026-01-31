using System.Text.Json.Serialization;

namespace WfpTrafficControl.Shared.Ipc;

/// <summary>
/// Request to set (enable) file watching on a policy file.
/// Request: { "type": "watch-set", "policyPath": "C:\\path\\to\\policy.json" }
/// </summary>
public sealed class WatchSetRequest : IpcRequest
{
    public const string RequestType = "watch-set";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// The absolute path to the policy file to watch.
    /// </summary>
    [JsonPropertyName("policyPath")]
    public string? PolicyPath { get; set; }
}

/// <summary>
/// Response to a watch-set request.
/// Response: { "ok": true, "watching": true, "policyPath": "..." }
/// </summary>
public sealed class WatchSetResponse : IpcResponse
{
    /// <summary>
    /// Whether file watching is now active.
    /// </summary>
    [JsonPropertyName("watching")]
    public bool Watching { get; set; }

    /// <summary>
    /// The path being watched (null if disabled).
    /// </summary>
    [JsonPropertyName("policyPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyPath { get; set; }

    /// <summary>
    /// Whether the initial policy was applied successfully.
    /// </summary>
    [JsonPropertyName("initialApplySuccess")]
    public bool InitialApplySuccess { get; set; }

    /// <summary>
    /// Warning message if initial apply had issues (non-fatal).
    /// </summary>
    [JsonPropertyName("warning")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Warning { get; set; }

    /// <summary>
    /// Creates a successful watch-set response.
    /// </summary>
    public static WatchSetResponse Success(bool watching, string? policyPath, bool initialApplySuccess, string? warning = null)
    {
        return new WatchSetResponse
        {
            Ok = true,
            Watching = watching,
            PolicyPath = policyPath,
            InitialApplySuccess = initialApplySuccess,
            Warning = warning
        };
    }

    /// <summary>
    /// Creates a response for disabling watch.
    /// </summary>
    public static WatchSetResponse Disabled()
    {
        return new WatchSetResponse
        {
            Ok = true,
            Watching = false,
            PolicyPath = null,
            InitialApplySuccess = false
        };
    }

    /// <summary>
    /// Creates a failed watch-set response.
    /// </summary>
    public static WatchSetResponse Failure(string error)
    {
        return new WatchSetResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Request to get the current watch status.
/// Request: { "type": "watch-status" }
/// </summary>
public sealed class WatchStatusRequest : IpcRequest
{
    public const string RequestType = "watch-status";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Response to a watch-status request.
/// Response: { "ok": true, "watching": true, "policyPath": "...", "lastApplyTime": "...", ... }
/// </summary>
public sealed class WatchStatusResponse : IpcResponse
{
    /// <summary>
    /// Whether file watching is currently active.
    /// </summary>
    [JsonPropertyName("watching")]
    public bool Watching { get; set; }

    /// <summary>
    /// The path being watched (null if not watching).
    /// </summary>
    [JsonPropertyName("policyPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyPath { get; set; }

    /// <summary>
    /// The debounce interval in milliseconds.
    /// </summary>
    [JsonPropertyName("debounceMs")]
    public int DebounceMs { get; set; }

    /// <summary>
    /// When the policy was last successfully applied (ISO 8601).
    /// </summary>
    [JsonPropertyName("lastApplyTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastApplyTime { get; set; }

    /// <summary>
    /// The last error message (if any).
    /// </summary>
    [JsonPropertyName("lastError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastError { get; set; }

    /// <summary>
    /// When the last error occurred (ISO 8601).
    /// </summary>
    [JsonPropertyName("lastErrorTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastErrorTime { get; set; }

    /// <summary>
    /// Number of successful applies since watch started.
    /// </summary>
    [JsonPropertyName("applyCount")]
    public int ApplyCount { get; set; }

    /// <summary>
    /// Number of failed applies since watch started.
    /// </summary>
    [JsonPropertyName("errorCount")]
    public int ErrorCount { get; set; }

    /// <summary>
    /// Creates a successful status response.
    /// </summary>
    public static WatchStatusResponse Success(
        bool watching,
        string? policyPath,
        int debounceMs,
        DateTime? lastApplyTime,
        string? lastError,
        DateTime? lastErrorTime,
        int applyCount,
        int errorCount)
    {
        return new WatchStatusResponse
        {
            Ok = true,
            Watching = watching,
            PolicyPath = policyPath,
            DebounceMs = debounceMs,
            LastApplyTime = lastApplyTime?.ToString("o"),
            LastError = lastError,
            LastErrorTime = lastErrorTime?.ToString("o"),
            ApplyCount = applyCount,
            ErrorCount = errorCount
        };
    }

    /// <summary>
    /// Creates a failed status response.
    /// </summary>
    public static WatchStatusResponse Failure(string error)
    {
        return new WatchStatusResponse
        {
            Ok = false,
            Error = error
        };
    }
}
