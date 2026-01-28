// src/shared/Policy/PolicyModels.cs
// Policy schema types for WFP Traffic Control
// Phase 11: Policy Schema v1

using System.Text.Json;
using System.Text.Json.Serialization;

namespace WfpTrafficControl.Shared.Policy;

/// <summary>
/// Root policy object containing rules and metadata.
/// </summary>
public sealed class Policy
{
    /// <summary>
    /// Policy schema version (semantic version format, e.g., "1.0.0").
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Default action when no rules match: "allow" or "block".
    /// </summary>
    [JsonPropertyName("defaultAction")]
    public string DefaultAction { get; set; } = "allow";

    /// <summary>
    /// ISO 8601 timestamp when policy was last updated.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Ordered list of rules. First matching rule wins (unless priority differs).
    /// </summary>
    [JsonPropertyName("rules")]
    public List<Rule> Rules { get; set; } = new();

    /// <summary>
    /// Deserialize a policy from JSON string.
    /// </summary>
    public static Policy? FromJson(string json)
    {
        return JsonSerializer.Deserialize<Policy>(json, SerializerOptions);
    }

    /// <summary>
    /// Serialize policy to JSON string.
    /// </summary>
    public string ToJson(bool indented = false)
    {
        var options = indented
            ? new JsonSerializerOptions(SerializerOptions) { WriteIndented = true }
            : SerializerOptions;
        return JsonSerializer.Serialize(this, options);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>
/// A single firewall rule specifying match criteria and action.
/// </summary>
public sealed class Rule
{
    /// <summary>
    /// Unique identifier for this rule (stable across policy updates).
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Action to take when rule matches: "allow" or "block".
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Traffic direction: "inbound", "outbound", or "both".
    /// </summary>
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// Protocol to match: "tcp", "udp", or "any".
    /// </summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    /// <summary>
    /// Optional process path to match (full path or image name).
    /// </summary>
    [JsonPropertyName("process")]
    public string? Process { get; set; }

    /// <summary>
    /// Optional local endpoint filter (IP/CIDR and/or ports).
    /// </summary>
    [JsonPropertyName("local")]
    public EndpointFilter? Local { get; set; }

    /// <summary>
    /// Optional remote endpoint filter (IP/CIDR and/or ports).
    /// </summary>
    [JsonPropertyName("remote")]
    public EndpointFilter? Remote { get; set; }

    /// <summary>
    /// Priority of this rule. Higher values take precedence.
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    /// <summary>
    /// Whether this rule is currently active.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional human-readable comment describing the rule.
    /// </summary>
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

/// <summary>
/// Filter criteria for an endpoint (local or remote).
/// </summary>
public sealed class EndpointFilter
{
    /// <summary>
    /// IP address or CIDR notation (e.g., "192.168.1.0/24", "10.0.0.1", "::1/128").
    /// </summary>
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    /// <summary>
    /// Port specification: single port, range, or comma-separated list.
    /// Examples: "80", "80-443", "80,443,8080-8090"
    /// </summary>
    [JsonPropertyName("ports")]
    public string? Ports { get; set; }
}

/// <summary>
/// Valid values for Rule.Action field.
/// </summary>
public static class RuleAction
{
    public const string Allow = "allow";
    public const string Block = "block";

    public static readonly string[] ValidValues = { Allow, Block };

    public static bool IsValid(string? value) =>
        value != null && ValidValues.Contains(value, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Valid values for Rule.Direction field.
/// </summary>
public static class RuleDirection
{
    public const string Inbound = "inbound";
    public const string Outbound = "outbound";
    public const string Both = "both";

    public static readonly string[] ValidValues = { Inbound, Outbound, Both };

    public static bool IsValid(string? value) =>
        value != null && ValidValues.Contains(value, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Valid values for Rule.Protocol field.
/// </summary>
public static class RuleProtocol
{
    public const string Tcp = "tcp";
    public const string Udp = "udp";
    public const string Any = "any";

    public static readonly string[] ValidValues = { Tcp, Udp, Any };

    public static bool IsValid(string? value) =>
        value != null && ValidValues.Contains(value, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Valid values for Policy.DefaultAction field.
/// </summary>
public static class DefaultAction
{
    public const string Allow = "allow";
    public const string Block = "block";

    public static readonly string[] ValidValues = { Allow, Block };

    public static bool IsValid(string? value) =>
        value != null && ValidValues.Contains(value, StringComparer.OrdinalIgnoreCase);
}
