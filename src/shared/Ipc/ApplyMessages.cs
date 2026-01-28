// src/shared/Ipc/ApplyMessages.cs
// IPC messages for policy application
// Phase 12: Compile Outbound TCP Rules

using System.Text.Json.Serialization;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Shared.Ipc;

/// <summary>
/// Request to apply a policy file.
/// Request: { "type": "apply", "policyPath": "C:\\path\\to\\policy.json" }
/// </summary>
public sealed class ApplyRequest : IpcRequest
{
    public const string RequestType = "apply";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// Full path to the policy JSON file to apply.
    /// </summary>
    [JsonPropertyName("policyPath")]
    public string PolicyPath { get; set; } = string.Empty;
}

/// <summary>
/// Response to an apply request.
/// Response: { "ok": true, "filtersCreated": 5, "filtersRemoved": 3, "warnings": [] }
/// </summary>
public sealed class ApplyResponse : IpcResponse
{
    /// <summary>
    /// Number of WFP filters created.
    /// </summary>
    [JsonPropertyName("filtersCreated")]
    public int FiltersCreated { get; set; }

    /// <summary>
    /// Number of WFP filters removed (from previous policy).
    /// </summary>
    [JsonPropertyName("filtersRemoved")]
    public int FiltersRemoved { get; set; }

    /// <summary>
    /// Number of rules that were skipped (disabled rules).
    /// </summary>
    [JsonPropertyName("rulesSkipped")]
    public int RulesSkipped { get; set; }

    /// <summary>
    /// Policy version that was applied.
    /// </summary>
    [JsonPropertyName("policyVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PolicyVersion { get; set; }

    /// <summary>
    /// Total rules in the policy.
    /// </summary>
    [JsonPropertyName("totalRules")]
    public int TotalRules { get; set; }

    /// <summary>
    /// Warnings generated during compilation (e.g., disabled rules).
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Compilation errors if any rules failed to compile.
    /// </summary>
    [JsonPropertyName("compilationErrors")]
    public List<ApplyCompilationErrorDto> CompilationErrors { get; set; } = new();

    /// <summary>
    /// Creates a successful apply response.
    /// </summary>
    public static ApplyResponse Success(
        int filtersCreated,
        int filtersRemoved,
        int rulesSkipped,
        string? policyVersion,
        int totalRules,
        List<string> warnings)
    {
        return new ApplyResponse
        {
            Ok = true,
            FiltersCreated = filtersCreated,
            FiltersRemoved = filtersRemoved,
            RulesSkipped = rulesSkipped,
            PolicyVersion = policyVersion,
            TotalRules = totalRules,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Creates a failed apply response due to compilation errors.
    /// </summary>
    public static ApplyResponse CompilationFailed(CompilationResult result)
    {
        return new ApplyResponse
        {
            Ok = false,
            Error = $"Policy compilation failed with {result.Errors.Count} error(s)",
            CompilationErrors = result.Errors.Select(e => new ApplyCompilationErrorDto
            {
                RuleId = e.RuleId,
                Message = e.Message
            }).ToList(),
            Warnings = result.Warnings
        };
    }

    /// <summary>
    /// Creates a failed apply response (general error).
    /// </summary>
    public static ApplyResponse Failure(string error)
    {
        return new ApplyResponse
        {
            Ok = false,
            Error = error
        };
    }
}

/// <summary>
/// DTO for compilation errors in responses.
/// </summary>
public sealed class ApplyCompilationErrorDto
{
    /// <summary>
    /// Rule ID that failed to compile.
    /// </summary>
    [JsonPropertyName("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    /// <summary>
    /// Error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
