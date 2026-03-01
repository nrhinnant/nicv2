using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.UI.Services;

/// <summary>
/// Service for comparing two policies and computing differences.
/// </summary>
public class PolicyDiffService
{
    /// <summary>
    /// Compares two policies and returns a diff result.
    /// </summary>
    public PolicyDiffResult Compare(Policy? left, Policy? right)
    {
        var result = new PolicyDiffResult();

        if (left == null && right == null)
            return result;

        if (left == null)
        {
            // All rules in right are added
            foreach (var rule in right!.Rules)
            {
                result.AddedRules.Add(new RuleDiff { Rule = rule });
            }
            result.DefaultActionChanged = true;
            result.OldDefaultAction = null;
            result.NewDefaultAction = right.DefaultAction;
            return result;
        }

        if (right == null)
        {
            // All rules in left are removed
            foreach (var rule in left.Rules)
            {
                result.RemovedRules.Add(new RuleDiff { Rule = rule });
            }
            result.DefaultActionChanged = true;
            result.OldDefaultAction = left.DefaultAction;
            result.NewDefaultAction = null;
            return result;
        }

        // Build lookup tables
        var leftRules = left.Rules.ToDictionary(r => r.Id, r => r);
        var rightRules = right.Rules.ToDictionary(r => r.Id, r => r);

        // Check for default action change
        if (left.DefaultAction != right.DefaultAction)
        {
            result.DefaultActionChanged = true;
            result.OldDefaultAction = left.DefaultAction;
            result.NewDefaultAction = right.DefaultAction;
        }

        // Check for version change
        if (left.Version != right.Version)
        {
            result.VersionChanged = true;
            result.OldVersion = left.Version;
            result.NewVersion = right.Version;
        }

        // Find added rules (in right but not in left)
        foreach (var rule in right.Rules)
        {
            if (!leftRules.ContainsKey(rule.Id))
            {
                result.AddedRules.Add(new RuleDiff { Rule = rule });
            }
        }

        // Find removed rules (in left but not in right)
        foreach (var rule in left.Rules)
        {
            if (!rightRules.ContainsKey(rule.Id))
            {
                result.RemovedRules.Add(new RuleDiff { Rule = rule });
            }
        }

        // Find modified rules (same ID, different content)
        foreach (var rule in left.Rules)
        {
            if (rightRules.TryGetValue(rule.Id, out var rightRule))
            {
                var changes = CompareRules(rule, rightRule);
                if (changes.Count > 0)
                {
                    result.ModifiedRules.Add(new ModifiedRuleDiff
                    {
                        OldRule = rule,
                        NewRule = rightRule,
                        ChangedFields = changes
                    });
                }
                else
                {
                    result.UnchangedRules.Add(new RuleDiff { Rule = rule });
                }
            }
        }

        return result;
    }

    private List<string> CompareRules(Rule left, Rule right)
    {
        var changes = new List<string>();

        if (left.Action != right.Action)
            changes.Add($"action: {left.Action} → {right.Action}");

        if (left.Direction != right.Direction)
            changes.Add($"direction: {left.Direction} → {right.Direction}");

        if (left.Protocol != right.Protocol)
            changes.Add($"protocol: {left.Protocol} → {right.Protocol}");

        if (left.Process != right.Process)
            changes.Add($"process: {left.Process ?? "(none)"} → {right.Process ?? "(none)"}");

        if (left.Priority != right.Priority)
            changes.Add($"priority: {left.Priority} → {right.Priority}");

        if (left.Enabled != right.Enabled)
            changes.Add($"enabled: {left.Enabled} → {right.Enabled}");

        if (left.Comment != right.Comment)
            changes.Add($"comment: {left.Comment ?? "(none)"} → {right.Comment ?? "(none)"}");

        // Compare remote endpoint
        var leftRemoteIp = left.Remote?.Ip;
        var rightRemoteIp = right.Remote?.Ip;
        if (leftRemoteIp != rightRemoteIp)
            changes.Add($"remote IP: {leftRemoteIp ?? "*"} → {rightRemoteIp ?? "*"}");

        var leftRemotePorts = left.Remote?.Ports;
        var rightRemotePorts = right.Remote?.Ports;
        if (leftRemotePorts != rightRemotePorts)
            changes.Add($"remote ports: {leftRemotePorts ?? "*"} → {rightRemotePorts ?? "*"}");

        // Compare local endpoint
        var leftLocalIp = left.Local?.Ip;
        var rightLocalIp = right.Local?.Ip;
        if (leftLocalIp != rightLocalIp)
            changes.Add($"local IP: {leftLocalIp ?? "*"} → {rightLocalIp ?? "*"}");

        var leftLocalPorts = left.Local?.Ports;
        var rightLocalPorts = right.Local?.Ports;
        if (leftLocalPorts != rightLocalPorts)
            changes.Add($"local ports: {leftLocalPorts ?? "*"} → {rightLocalPorts ?? "*"}");

        return changes;
    }
}

/// <summary>
/// Result of comparing two policies.
/// </summary>
public class PolicyDiffResult
{
    public List<RuleDiff> AddedRules { get; } = new();
    public List<RuleDiff> RemovedRules { get; } = new();
    public List<ModifiedRuleDiff> ModifiedRules { get; } = new();
    public List<RuleDiff> UnchangedRules { get; } = new();

    public bool DefaultActionChanged { get; set; }
    public string? OldDefaultAction { get; set; }
    public string? NewDefaultAction { get; set; }

    public bool VersionChanged { get; set; }
    public string? OldVersion { get; set; }
    public string? NewVersion { get; set; }

    public bool HasChanges => AddedRules.Count > 0 || RemovedRules.Count > 0 ||
                               ModifiedRules.Count > 0 || DefaultActionChanged || VersionChanged;

    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (AddedRules.Count > 0)
                parts.Add($"{AddedRules.Count} added");
            if (RemovedRules.Count > 0)
                parts.Add($"{RemovedRules.Count} removed");
            if (ModifiedRules.Count > 0)
                parts.Add($"{ModifiedRules.Count} modified");
            if (UnchangedRules.Count > 0)
                parts.Add($"{UnchangedRules.Count} unchanged");
            if (DefaultActionChanged)
                parts.Add("default action changed");
            if (VersionChanged)
                parts.Add("version changed");

            return parts.Count > 0 ? string.Join(", ", parts) : "No changes";
        }
    }
}

/// <summary>
/// Represents a single rule in a diff.
/// </summary>
public class RuleDiff
{
    public required Rule Rule { get; set; }
}

/// <summary>
/// Represents a modified rule with details about what changed.
/// </summary>
public class ModifiedRuleDiff
{
    public required Rule OldRule { get; set; }
    public required Rule NewRule { get; set; }
    public List<string> ChangedFields { get; set; } = new();
}
