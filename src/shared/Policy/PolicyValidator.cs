// src/shared/Policy/PolicyValidator.cs
// Strict policy validation
// Phase 11: Policy Schema v1

using System.Text.Json;

namespace WfpTrafficControl.Shared.Policy;

/// <summary>
/// Result of policy validation containing all errors found.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Whether the policy is valid (no errors).
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// List of validation errors found.
    /// </summary>
    public List<ValidationError> Errors { get; } = new();

    /// <summary>
    /// Adds an error to the result.
    /// </summary>
    public void AddError(string path, string message)
    {
        Errors.Add(new ValidationError(path, message));
    }

    /// <summary>
    /// Adds an error with rule context.
    /// </summary>
    public void AddRuleError(int index, string ruleId, string field, string message)
    {
        var path = string.IsNullOrEmpty(ruleId)
            ? $"rules[{index}].{field}"
            : $"rules[{index}] (id='{ruleId}').{field}";
        Errors.Add(new ValidationError(path, message));
    }

    /// <summary>
    /// Gets a formatted summary of all errors.
    /// </summary>
    public string GetSummary()
    {
        if (IsValid)
            return "Policy is valid.";

        var lines = new List<string>
        {
            $"Policy validation failed with {Errors.Count} error(s):",
            ""
        };

        foreach (var error in Errors)
        {
            lines.Add($"  - {error.Path}: {error.Message}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// A single validation error with path and message.
/// </summary>
public sealed record ValidationError(string Path, string Message);

/// <summary>
/// Validates policy documents against the schema.
/// </summary>
public static class PolicyValidator
{
    /// <summary>
    /// Maximum allowed policy file size in bytes (1 MB).
    /// </summary>
    public const int MaxPolicyFileSize = 1024 * 1024;

    /// <summary>
    /// Maximum allowed number of rules in a policy.
    /// </summary>
    public const int MaxRuleCount = 10000;

    /// <summary>
    /// Maximum length of a rule ID.
    /// </summary>
    public const int MaxRuleIdLength = 128;

    /// <summary>
    /// Maximum length of a comment.
    /// </summary>
    public const int MaxCommentLength = 1024;

    /// <summary>
    /// Maximum length of a process path.
    /// </summary>
    public const int MaxProcessPathLength = 260;

    /// <summary>
    /// Validates a policy JSON string.
    /// </summary>
    /// <param name="json">JSON string to validate</param>
    /// <returns>Validation result with all errors found</returns>
    public static ValidationResult ValidateJson(string json)
    {
        var result = new ValidationResult();

        // Check for null/empty
        if (string.IsNullOrWhiteSpace(json))
        {
            result.AddError("(root)", "Policy JSON cannot be empty");
            return result;
        }

        // Check size limit (using byte count for accurate UTF-8 size check)
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(json);
        if (byteCount > MaxPolicyFileSize)
        {
            result.AddError("(root)", $"Policy JSON exceeds maximum size ({MaxPolicyFileSize / 1024} KB, actual: {byteCount / 1024} KB)");
            return result;
        }

        // Try to parse JSON
        Policy? policy;
        try
        {
            policy = Policy.FromJson(json);
        }
        catch (JsonException ex)
        {
            result.AddError("(root)", $"Invalid JSON: {ex.Message}");
            return result;
        }

        if (policy == null)
        {
            result.AddError("(root)", "Failed to parse policy (null result)");
            return result;
        }

        // Validate the parsed policy
        ValidatePolicy(policy, result);

        return result;
    }

    /// <summary>
    /// Validates a parsed policy object.
    /// </summary>
    /// <param name="policy">Policy to validate</param>
    /// <returns>Validation result with all errors found</returns>
    public static ValidationResult Validate(Policy policy)
    {
        var result = new ValidationResult();

        if (policy == null)
        {
            result.AddError("(root)", "Policy cannot be null");
            return result;
        }

        ValidatePolicy(policy, result);
        return result;
    }

    /// <summary>
    /// Internal validation logic.
    /// </summary>
    private static void ValidatePolicy(Policy policy, ValidationResult result)
    {
        // Validate version
        if (string.IsNullOrWhiteSpace(policy.Version))
        {
            result.AddError("version", "Version is required");
        }
        else if (!NetworkUtils.ValidateVersion(policy.Version, out var versionError))
        {
            result.AddError("version", versionError!);
        }

        // Validate defaultAction
        if (string.IsNullOrWhiteSpace(policy.DefaultAction))
        {
            result.AddError("defaultAction", "Default action is required");
        }
        else if (!DefaultAction.IsValid(policy.DefaultAction))
        {
            result.AddError("defaultAction",
                $"Invalid default action: '{policy.DefaultAction}'. Must be one of: {string.Join(", ", DefaultAction.ValidValues)}");
        }

        // Validate updatedAt
        if (policy.UpdatedAt == default)
        {
            result.AddError("updatedAt", "Updated timestamp is required");
        }
        else if (policy.UpdatedAt > DateTime.UtcNow.AddMinutes(5))
        {
            result.AddError("updatedAt", "Updated timestamp cannot be in the future");
        }

        // Validate rules
        if (policy.Rules == null)
        {
            result.AddError("rules", "Rules list is required (can be empty)");
            return;
        }

        if (policy.Rules.Count > MaxRuleCount)
        {
            result.AddError("rules", $"Too many rules: {policy.Rules.Count} (max {MaxRuleCount})");
        }

        // Track rule IDs for duplicate detection
        var ruleIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < policy.Rules.Count; i++)
        {
            var rule = policy.Rules[i];
            if (rule == null)
            {
                result.AddError($"rules[{i}]", "Rule cannot be null");
                continue;
            }

            ValidateRule(rule, i, ruleIds, result);
        }
    }

    /// <summary>
    /// Validates a single rule.
    /// </summary>
    private static void ValidateRule(Rule rule, int index, Dictionary<string, int> ruleIds, ValidationResult result)
    {
        var ruleId = rule.Id ?? "";

        // Validate id (required)
        if (string.IsNullOrWhiteSpace(rule.Id))
        {
            result.AddRuleError(index, ruleId, "id", "Rule ID is required");
        }
        else
        {
            if (rule.Id.Length > MaxRuleIdLength)
            {
                result.AddRuleError(index, ruleId, "id",
                    $"Rule ID exceeds maximum length ({MaxRuleIdLength} characters)");
            }

            // Check for duplicate IDs
            if (ruleIds.TryGetValue(rule.Id, out var existingIndex))
            {
                result.AddRuleError(index, ruleId, "id",
                    $"Duplicate rule ID. First occurrence at rules[{existingIndex}]");
            }
            else
            {
                ruleIds[rule.Id] = index;
            }

            // Check for valid ID format (alphanumeric, dashes, underscores)
            if (!IsValidRuleId(rule.Id))
            {
                result.AddRuleError(index, ruleId, "id",
                    "Rule ID must contain only alphanumeric characters, dashes, and underscores");
            }
        }

        // Validate action (required)
        if (string.IsNullOrWhiteSpace(rule.Action))
        {
            result.AddRuleError(index, ruleId, "action", "Action is required");
        }
        else if (!RuleAction.IsValid(rule.Action))
        {
            result.AddRuleError(index, ruleId, "action",
                $"Invalid action: '{rule.Action}'. Must be one of: {string.Join(", ", RuleAction.ValidValues)}");
        }

        // Validate direction (required)
        if (string.IsNullOrWhiteSpace(rule.Direction))
        {
            result.AddRuleError(index, ruleId, "direction", "Direction is required");
        }
        else if (!RuleDirection.IsValid(rule.Direction))
        {
            result.AddRuleError(index, ruleId, "direction",
                $"Invalid direction: '{rule.Direction}'. Must be one of: {string.Join(", ", RuleDirection.ValidValues)}");
        }

        // Validate protocol (required)
        if (string.IsNullOrWhiteSpace(rule.Protocol))
        {
            result.AddRuleError(index, ruleId, "protocol", "Protocol is required");
        }
        else if (!RuleProtocol.IsValid(rule.Protocol))
        {
            result.AddRuleError(index, ruleId, "protocol",
                $"Invalid protocol: '{rule.Protocol}'. Must be one of: {string.Join(", ", RuleProtocol.ValidValues)}");
        }

        // Validate process (optional)
        if (!string.IsNullOrEmpty(rule.Process))
        {
            if (rule.Process.Length > MaxProcessPathLength)
            {
                result.AddRuleError(index, ruleId, "process",
                    $"Process path exceeds maximum length ({MaxProcessPathLength} characters)");
            }
            else if (!NetworkUtils.ValidateProcessPath(rule.Process, out var processError))
            {
                result.AddRuleError(index, ruleId, "process", processError!);
            }
        }

        // Validate local endpoint (optional)
        if (rule.Local != null)
        {
            ValidateEndpointFilter(rule.Local, index, ruleId, "local", result);
        }

        // Validate remote endpoint (optional)
        if (rule.Remote != null)
        {
            ValidateEndpointFilter(rule.Remote, index, ruleId, "remote", result);
        }

        // Validate comment (optional)
        if (!string.IsNullOrEmpty(rule.Comment) && rule.Comment.Length > MaxCommentLength)
        {
            result.AddRuleError(index, ruleId, "comment",
                $"Comment exceeds maximum length ({MaxCommentLength} characters)");
        }

        // Note: priority and enabled are validated by their types (int and bool)
        // No additional validation needed for these fields
    }

    /// <summary>
    /// Validates an endpoint filter (local or remote).
    /// </summary>
    private static void ValidateEndpointFilter(EndpointFilter filter, int ruleIndex, string ruleId,
        string fieldName, ValidationResult result)
    {
        // At least one of ip or ports should be specified
        if (string.IsNullOrEmpty(filter.Ip) && string.IsNullOrEmpty(filter.Ports))
        {
            result.AddRuleError(ruleIndex, ruleId, fieldName,
                "Endpoint filter must specify at least 'ip' or 'ports'");
            return;
        }

        // Validate IP/CIDR if specified
        if (!string.IsNullOrEmpty(filter.Ip))
        {
            if (!NetworkUtils.ValidateIpOrCidr(filter.Ip, out var ipError))
            {
                result.AddRuleError(ruleIndex, ruleId, $"{fieldName}.ip", ipError!);
            }
        }

        // Validate ports if specified
        if (!string.IsNullOrEmpty(filter.Ports))
        {
            if (!NetworkUtils.ValidatePorts(filter.Ports, out var portsError))
            {
                result.AddRuleError(ruleIndex, ruleId, $"{fieldName}.ports", portsError!);
            }
        }
    }

    /// <summary>
    /// Checks if a rule ID contains only valid characters.
    /// </summary>
    private static bool IsValidRuleId(string id)
    {
        foreach (char c in id)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                return false;
        }
        return true;
    }
}
