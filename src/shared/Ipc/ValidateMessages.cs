// src/shared/Ipc/ValidateMessages.cs
// IPC messages for policy validation
// Phase 11: Policy Schema v1

using System.Text.Json.Serialization;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Shared.Ipc;

/// <summary>
/// Request to validate a policy.
/// Request: { "type": "validate", "policyJson": "..." }
/// </summary>
public sealed class ValidateRequest : IpcRequest
{
    public const string RequestType = "validate";

    [JsonPropertyName("type")]
    public override string Type => RequestType;

    /// <summary>
    /// The policy JSON string to validate.
    /// </summary>
    [JsonPropertyName("policyJson")]
    public string PolicyJson { get; set; } = string.Empty;
}

/// <summary>
/// Response to a validate request.
/// Response: { "ok": true, "valid": true, "errors": [], "ruleCount": 5 }
/// </summary>
public sealed class ValidateResponse : IpcResponse
{
    /// <summary>
    /// True if the policy is valid.
    /// </summary>
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    /// <summary>
    /// List of validation errors (if any).
    /// </summary>
    [JsonPropertyName("errors")]
    public List<ValidationErrorDto> Errors { get; set; } = new();

    /// <summary>
    /// Number of rules in the policy (if valid).
    /// </summary>
    [JsonPropertyName("ruleCount")]
    public int RuleCount { get; set; }

    /// <summary>
    /// Policy version (if valid).
    /// </summary>
    [JsonPropertyName("version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; set; }

    /// <summary>
    /// Creates a successful response for a valid policy.
    /// </summary>
    public static ValidateResponse ForValidPolicy(Policy.Policy policy)
    {
        return new ValidateResponse
        {
            Ok = true,
            Valid = true,
            RuleCount = policy.Rules.Count,
            Version = policy.Version
        };
    }

    /// <summary>
    /// Creates a successful response for an invalid policy.
    /// </summary>
    public static ValidateResponse ForInvalidPolicy(ValidationResult result)
    {
        return new ValidateResponse
        {
            Ok = true,
            Valid = false,
            Errors = result.Errors.Select(e => new ValidationErrorDto
            {
                Path = e.Path,
                Message = e.Message
            }).ToList()
        };
    }

    /// <summary>
    /// Creates a failed response (service error, not validation error).
    /// </summary>
    public static ValidateResponse Failure(string error)
    {
        return new ValidateResponse
        {
            Ok = false,
            Valid = false,
            Error = error
        };
    }
}

/// <summary>
/// DTO for validation errors in responses.
/// </summary>
public sealed class ValidationErrorDto
{
    /// <summary>
    /// JSON path to the error location (e.g., "rules[0].id").
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
