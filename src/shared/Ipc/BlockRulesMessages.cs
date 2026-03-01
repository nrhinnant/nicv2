using System.Text.Json.Serialization;

namespace WfpTrafficControl.Shared.Ipc;

/// <summary>
/// Request to retrieve current block rules from the active policy.
/// Request: { "type": "block-rules" }
/// </summary>
public sealed class BlockRulesRequest : IpcRequest
{
    public const string RequestType = "block-rules";

    [JsonPropertyName("type")]
    public override string Type => RequestType;
}

/// <summary>
/// Response containing current block rules.
/// </summary>
public sealed class BlockRulesResponse : IpcResponse
{
    /// <summary>
    /// List of block rules from the current policy.
    /// </summary>
    [JsonPropertyName("rules")]
    public List<BlockRuleDto> Rules { get; set; } = new();

    /// <summary>
    /// Total number of block rules.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>
    /// Policy version these rules are from.
    /// </summary>
    [JsonPropertyName("policyVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyVersion { get; set; }

    /// <summary>
    /// Whether a policy is currently loaded.
    /// </summary>
    [JsonPropertyName("policyLoaded")]
    public bool PolicyLoaded { get; set; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    public static BlockRulesResponse Success(List<BlockRuleDto> rules, string? policyVersion)
    {
        return new BlockRulesResponse
        {
            Ok = true,
            Rules = rules,
            Count = rules.Count,
            PolicyVersion = policyVersion,
            PolicyLoaded = true
        };
    }

    /// <summary>
    /// Creates a response for when no policy is loaded.
    /// </summary>
    public static BlockRulesResponse NoPolicyLoaded()
    {
        return new BlockRulesResponse
        {
            Ok = true,
            Rules = new List<BlockRuleDto>(),
            Count = 0,
            PolicyLoaded = false
        };
    }

    /// <summary>
    /// Creates a failed response.
    /// </summary>
    public static BlockRulesResponse Failure(string error)
    {
        return new BlockRulesResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// DTO for a block rule.
/// </summary>
public sealed class BlockRuleDto
{
    /// <summary>
    /// Rule identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Direction (inbound, outbound, both).
    /// </summary>
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Protocol (tcp, udp, any).
    /// </summary>
    [JsonPropertyName("protocol")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Protocol { get; set; }

    /// <summary>
    /// Process path being blocked.
    /// </summary>
    [JsonPropertyName("process")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Process { get; set; }

    /// <summary>
    /// Remote IP or CIDR being blocked.
    /// </summary>
    [JsonPropertyName("remoteIp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RemoteIp { get; set; }

    /// <summary>
    /// Remote ports being blocked.
    /// </summary>
    [JsonPropertyName("remotePorts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RemotePorts { get; set; }

    /// <summary>
    /// Local IP or CIDR being blocked.
    /// </summary>
    [JsonPropertyName("localIp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LocalIp { get; set; }

    /// <summary>
    /// Local ports being blocked.
    /// </summary>
    [JsonPropertyName("localPorts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LocalPorts { get; set; }

    /// <summary>
    /// Rule comment/description.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; set; }

    /// <summary>
    /// Rule priority.
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    /// <summary>
    /// Whether the rule is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Human-readable summary of what this rule blocks.
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}
