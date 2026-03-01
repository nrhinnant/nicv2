using System.Text.Json.Serialization;

namespace WfpTrafficControl.Shared.Ipc;

/// <summary>
/// Request to simulate a connection against the current policy.
/// Request: { "type": "simulate", "direction": "outbound", "protocol": "tcp", ... }
/// </summary>
public sealed class SimulateRequest : IpcRequest
{
    public const string RequestType = "simulate";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// Traffic direction: "inbound" or "outbound".
    /// </summary>
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "outbound";

    /// <summary>
    /// Protocol: "tcp" or "udp".
    /// </summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = "tcp";

    /// <summary>
    /// Process path (optional). Full path to the executable.
    /// </summary>
    [JsonPropertyName("processPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProcessPath { get; set; }

    /// <summary>
    /// Local IP address (optional, for display purposes).
    /// </summary>
    [JsonPropertyName("localIp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LocalIp { get; set; }

    /// <summary>
    /// Local port (optional, for display purposes).
    /// </summary>
    [JsonPropertyName("localPort")]
    public int? LocalPort { get; set; }

    /// <summary>
    /// Remote IP address (required for meaningful simulation).
    /// </summary>
    [JsonPropertyName("remoteIp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RemoteIp { get; set; }

    /// <summary>
    /// Remote port (required for meaningful simulation).
    /// </summary>
    [JsonPropertyName("remotePort")]
    public int? RemotePort { get; set; }
}

/// <summary>
/// Response from a simulation request.
/// </summary>
public sealed class SimulateResponse : IpcResponse
{
    /// <summary>
    /// Whether the connection would be allowed.
    /// </summary>
    [JsonPropertyName("wouldAllow")]
    public bool WouldAllow { get; set; }

    /// <summary>
    /// The ID of the rule that matched (null if default action was used).
    /// </summary>
    [JsonPropertyName("matchedRuleId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MatchedRuleId { get; set; }

    /// <summary>
    /// The action of the matched rule ("allow" or "block").
    /// </summary>
    [JsonPropertyName("matchedAction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MatchedAction { get; set; }

    /// <summary>
    /// Comment from the matched rule, if any.
    /// </summary>
    [JsonPropertyName("matchedRuleComment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MatchedRuleComment { get; set; }

    /// <summary>
    /// Whether the default action was applied (no rule matched).
    /// </summary>
    [JsonPropertyName("usedDefaultAction")]
    public bool UsedDefaultAction { get; set; }

    /// <summary>
    /// The default action of the policy.
    /// </summary>
    [JsonPropertyName("defaultAction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultAction { get; set; }

    /// <summary>
    /// List of rules that were evaluated (for debugging/understanding).
    /// </summary>
    [JsonPropertyName("evaluationTrace")]
    public List<SimulateEvaluationStep> EvaluationTrace { get; set; } = new();

    /// <summary>
    /// Total number of rules evaluated.
    /// </summary>
    [JsonPropertyName("rulesEvaluated")]
    public int RulesEvaluated { get; set; }

    /// <summary>
    /// Whether a policy is loaded.
    /// </summary>
    [JsonPropertyName("policyLoaded")]
    public bool PolicyLoaded { get; set; }

    /// <summary>
    /// Policy version used for simulation.
    /// </summary>
    [JsonPropertyName("policyVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyVersion { get; set; }

    /// <summary>
    /// Creates a successful simulation response.
    /// </summary>
    public static SimulateResponse Success(
        bool wouldAllow,
        string? matchedRuleId,
        string? matchedAction,
        string? matchedRuleComment,
        bool usedDefaultAction,
        string? defaultAction,
        List<SimulateEvaluationStep> evaluationTrace,
        string? policyVersion)
    {
        return new SimulateResponse
        {
            Ok = true,
            WouldAllow = wouldAllow,
            MatchedRuleId = matchedRuleId,
            MatchedAction = matchedAction,
            MatchedRuleComment = matchedRuleComment,
            UsedDefaultAction = usedDefaultAction,
            DefaultAction = defaultAction,
            EvaluationTrace = evaluationTrace,
            RulesEvaluated = evaluationTrace.Count,
            PolicyLoaded = true,
            PolicyVersion = policyVersion
        };
    }

    /// <summary>
    /// Creates a response for when no policy is loaded.
    /// </summary>
    public static SimulateResponse NoPolicyLoaded()
    {
        return new SimulateResponse
        {
            Ok = true,
            WouldAllow = true, // Default allow when no policy
            UsedDefaultAction = true,
            DefaultAction = "allow",
            PolicyLoaded = false
        };
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static SimulateResponse Failure(string error)
    {
        return new SimulateResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Represents one step in the rule evaluation trace.
/// </summary>
public sealed class SimulateEvaluationStep
{
    /// <summary>
    /// Rule ID.
    /// </summary>
    [JsonPropertyName("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    /// <summary>
    /// Rule action (allow/block).
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Whether this rule matched.
    /// </summary>
    [JsonPropertyName("matched")]
    public bool Matched { get; set; }

    /// <summary>
    /// Reason why the rule did or didn't match.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Rule priority.
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}
