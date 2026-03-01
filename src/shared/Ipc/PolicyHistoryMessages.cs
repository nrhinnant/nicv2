using System.Text.Json.Serialization;
using WfpTrafficControl.Shared.History;

namespace WfpTrafficControl.Shared.Ipc;

/// <summary>
/// Request to get policy history.
/// </summary>
public sealed class PolicyHistoryRequest : IpcRequest
{
    public const string RequestType = "policy-history";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// Maximum number of history entries to return (default 50).
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 50;
}

/// <summary>
/// Response containing policy history entries.
/// </summary>
public sealed class PolicyHistoryResponse : IpcResponse
{
    /// <summary>
    /// List of history entries (most recent first).
    /// </summary>
    [JsonPropertyName("entries")]
    public List<PolicyHistoryEntryDto> Entries { get; set; } = new();

    /// <summary>
    /// Total count of history entries available.
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    public static PolicyHistoryResponse Success(List<PolicyHistoryEntryDto> entries, int totalCount)
    {
        return new PolicyHistoryResponse
        {
            Ok = true,
            Entries = entries,
            TotalCount = totalCount
        };
    }

    public static PolicyHistoryResponse Failure(string error)
    {
        return new PolicyHistoryResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// DTO for policy history entry.
/// </summary>
public sealed class PolicyHistoryEntryDto
{
    /// <summary>
    /// Unique identifier for this history entry.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this policy was applied.
    /// </summary>
    [JsonPropertyName("appliedAt")]
    public DateTime AppliedAt { get; set; }

    /// <summary>
    /// Policy version from the policy file.
    /// </summary>
    [JsonPropertyName("policyVersion")]
    public string PolicyVersion { get; set; } = string.Empty;

    /// <summary>
    /// Number of rules in the policy.
    /// </summary>
    [JsonPropertyName("ruleCount")]
    public int RuleCount { get; set; }

    /// <summary>
    /// Source of the apply operation (CLI, UI, Watch, LKG).
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Original source file path (if applicable).
    /// </summary>
    [JsonPropertyName("sourcePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourcePath { get; set; }

    /// <summary>
    /// Number of filters created by this policy.
    /// </summary>
    [JsonPropertyName("filtersCreated")]
    public int FiltersCreated { get; set; }

    /// <summary>
    /// Number of filters removed when applying this policy.
    /// </summary>
    [JsonPropertyName("filtersRemoved")]
    public int FiltersRemoved { get; set; }

    /// <summary>
    /// Creates a DTO from a history entry.
    /// </summary>
    public static PolicyHistoryEntryDto FromEntry(PolicyHistoryEntry entry)
    {
        return new PolicyHistoryEntryDto
        {
            Id = entry.Id,
            AppliedAt = entry.AppliedAt,
            PolicyVersion = entry.PolicyVersion,
            RuleCount = entry.RuleCount,
            Source = entry.Source,
            SourcePath = entry.SourcePath,
            FiltersCreated = entry.FiltersCreated,
            FiltersRemoved = entry.FiltersRemoved
        };
    }
}

/// <summary>
/// Request to revert to a specific policy version from history.
/// </summary>
public sealed class PolicyHistoryRevertRequest : IpcRequest
{
    public const string RequestType = "policy-history-revert";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// ID of the history entry to revert to.
    /// </summary>
    [JsonPropertyName("entryId")]
    public string EntryId { get; set; } = string.Empty;
}

/// <summary>
/// Response to a policy history revert request.
/// </summary>
public sealed class PolicyHistoryRevertResponse : IpcResponse
{
    /// <summary>
    /// Number of WFP filters created.
    /// </summary>
    [JsonPropertyName("filtersCreated")]
    public int FiltersCreated { get; set; }

    /// <summary>
    /// Number of WFP filters removed.
    /// </summary>
    [JsonPropertyName("filtersRemoved")]
    public int FiltersRemoved { get; set; }

    /// <summary>
    /// Number of rules skipped (disabled rules).
    /// </summary>
    [JsonPropertyName("rulesSkipped")]
    public int RulesSkipped { get; set; }

    /// <summary>
    /// Policy version that was reverted to.
    /// </summary>
    [JsonPropertyName("policyVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyVersion { get; set; }

    /// <summary>
    /// Total rules in the reverted policy.
    /// </summary>
    [JsonPropertyName("totalRules")]
    public int TotalRules { get; set; }

    /// <summary>
    /// Entry ID that was reverted to.
    /// </summary>
    [JsonPropertyName("revertedToId")]
    public string RevertedToId { get; set; } = string.Empty;

    public static PolicyHistoryRevertResponse Success(
        int filtersCreated,
        int filtersRemoved,
        int rulesSkipped,
        string? policyVersion,
        int totalRules,
        string revertedToId)
    {
        return new PolicyHistoryRevertResponse
        {
            Ok = true,
            FiltersCreated = filtersCreated,
            FiltersRemoved = filtersRemoved,
            RulesSkipped = rulesSkipped,
            PolicyVersion = policyVersion,
            TotalRules = totalRules,
            RevertedToId = revertedToId
        };
    }

    public static PolicyHistoryRevertResponse NotFound(string entryId)
    {
        return new PolicyHistoryRevertResponse
        {
            Ok = false,
            Error = $"History entry not found: {entryId}"
        };
    }

    public static PolicyHistoryRevertResponse Failure(string error)
    {
        return new PolicyHistoryRevertResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// Request to get a specific policy from history.
/// </summary>
public sealed class PolicyHistoryGetRequest : IpcRequest
{
    public const string RequestType = "policy-history-get";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// ID of the history entry to get.
    /// </summary>
    [JsonPropertyName("entryId")]
    public string EntryId { get; set; } = string.Empty;
}

/// <summary>
/// Response containing a specific policy from history.
/// </summary>
public sealed class PolicyHistoryGetResponse : IpcResponse
{
    /// <summary>
    /// The history entry metadata.
    /// </summary>
    [JsonPropertyName("entry")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PolicyHistoryEntryDto? Entry { get; set; }

    /// <summary>
    /// The policy JSON content.
    /// </summary>
    [JsonPropertyName("policyJson")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyJson { get; set; }

    public static PolicyHistoryGetResponse Success(PolicyHistoryEntryDto entry, string policyJson)
    {
        return new PolicyHistoryGetResponse
        {
            Ok = true,
            Entry = entry,
            PolicyJson = policyJson
        };
    }

    public static PolicyHistoryGetResponse NotFound(string entryId)
    {
        return new PolicyHistoryGetResponse
        {
            Ok = false,
            Error = $"History entry not found: {entryId}"
        };
    }

    public static PolicyHistoryGetResponse Failure(string error)
    {
        return new PolicyHistoryGetResponse
        {
            Ok = false,
            Error = error
        };
    }
}
