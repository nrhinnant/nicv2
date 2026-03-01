// src/shared/Policy/RuleSimulator.cs
// Simulates connections against policy rules without affecting WFP

using System.Net;
using WfpTrafficControl.Shared.Ipc;

namespace WfpTrafficControl.Shared.Policy;

/// <summary>
/// Simulates connections against policy rules to determine what action would be taken.
/// This allows "what if" testing without actually applying any WFP changes.
/// </summary>
public static class RuleSimulator
{
    /// <summary>
    /// Simulates a connection against the given policy.
    /// </summary>
    /// <param name="policy">The policy to simulate against.</param>
    /// <param name="request">The connection parameters to simulate.</param>
    /// <returns>Simulation result with matched rule and evaluation trace.</returns>
    public static SimulateResponse Simulate(Policy policy, SimulateRequest request)
    {
        if (policy == null)
        {
            return SimulateResponse.NoPolicyLoaded();
        }

        var evaluationTrace = new List<SimulateEvaluationStep>();

        // Sort rules by priority (higher priority first) for proper evaluation order
        var sortedRules = policy.Rules
            .Where(r => r.Enabled)
            .OrderByDescending(r => r.Priority)
            .ToList();

        SimulateEvaluationStep? matchedStep = null;
        Rule? matchedRule = null;

        foreach (var rule in sortedRules)
        {
            var (matches, reason) = EvaluateRule(rule, request);

            var step = new SimulateEvaluationStep
            {
                RuleId = rule.Id ?? "unknown",
                Action = rule.Action ?? "allow",
                Matched = matches,
                Reason = reason,
                Priority = rule.Priority
            };

            evaluationTrace.Add(step);

            if (matches && matchedRule == null)
            {
                matchedStep = step;
                matchedRule = rule;
                // Continue evaluating for complete trace, but we have our answer
            }
        }

        // Determine the outcome
        if (matchedRule != null)
        {
            var wouldAllow = string.Equals(matchedRule.Action, RuleAction.Allow, StringComparison.OrdinalIgnoreCase);
            return SimulateResponse.Success(
                wouldAllow: wouldAllow,
                matchedRuleId: matchedRule.Id,
                matchedAction: matchedRule.Action,
                matchedRuleComment: matchedRule.Comment,
                usedDefaultAction: false,
                defaultAction: policy.DefaultAction,
                evaluationTrace: evaluationTrace,
                policyVersion: policy.Version);
        }
        else
        {
            // No rule matched, use default action
            var wouldAllow = string.Equals(policy.DefaultAction, DefaultAction.Allow, StringComparison.OrdinalIgnoreCase);
            return SimulateResponse.Success(
                wouldAllow: wouldAllow,
                matchedRuleId: null,
                matchedAction: null,
                matchedRuleComment: null,
                usedDefaultAction: true,
                defaultAction: policy.DefaultAction,
                evaluationTrace: evaluationTrace,
                policyVersion: policy.Version);
        }
    }

    /// <summary>
    /// Evaluates whether a single rule matches the given connection parameters.
    /// </summary>
    /// <returns>Tuple of (matches, reason).</returns>
    private static (bool Matches, string Reason) EvaluateRule(Rule rule, SimulateRequest request)
    {
        // Check direction
        if (!MatchesDirection(rule.Direction, request.Direction))
        {
            return (false, $"Direction mismatch: rule={rule.Direction}, connection={request.Direction}");
        }

        // Check protocol
        if (!MatchesProtocol(rule.Protocol, request.Protocol))
        {
            return (false, $"Protocol mismatch: rule={rule.Protocol}, connection={request.Protocol}");
        }

        // Check process (if specified in rule)
        if (!string.IsNullOrEmpty(rule.Process))
        {
            if (string.IsNullOrEmpty(request.ProcessPath))
            {
                return (false, "Rule requires process match but no process specified");
            }

            if (!MatchesProcess(rule.Process, request.ProcessPath))
            {
                return (false, $"Process mismatch: rule={Path.GetFileName(rule.Process)}, connection={Path.GetFileName(request.ProcessPath)}");
            }
        }

        // Check remote IP (if specified in rule)
        if (rule.Remote?.Ip != null)
        {
            if (string.IsNullOrEmpty(request.RemoteIp))
            {
                return (false, "Rule requires remote IP match but no remote IP specified");
            }

            if (!MatchesIpCidr(rule.Remote.Ip, request.RemoteIp))
            {
                return (false, $"Remote IP mismatch: rule={rule.Remote.Ip}, connection={request.RemoteIp}");
            }
        }

        // Check remote ports (if specified in rule)
        if (!string.IsNullOrEmpty(rule.Remote?.Ports))
        {
            if (!request.RemotePort.HasValue)
            {
                return (false, "Rule requires remote port match but no remote port specified");
            }

            if (!MatchesPorts(rule.Remote.Ports, request.RemotePort.Value))
            {
                return (false, $"Remote port mismatch: rule={rule.Remote.Ports}, connection={request.RemotePort}");
            }
        }

        // Check local IP (if specified in rule) - currently not fully supported in WFP layer
        // but we simulate it for completeness
        if (rule.Local?.Ip != null)
        {
            if (string.IsNullOrEmpty(request.LocalIp))
            {
                return (false, "Rule requires local IP match but no local IP specified");
            }

            if (!MatchesIpCidr(rule.Local.Ip, request.LocalIp))
            {
                return (false, $"Local IP mismatch: rule={rule.Local.Ip}, connection={request.LocalIp}");
            }
        }

        // Check local ports (if specified in rule) - currently not fully supported
        if (!string.IsNullOrEmpty(rule.Local?.Ports))
        {
            if (!request.LocalPort.HasValue)
            {
                return (false, "Rule requires local port match but no local port specified");
            }

            if (!MatchesPorts(rule.Local.Ports, request.LocalPort.Value))
            {
                return (false, $"Local port mismatch: rule={rule.Local.Ports}, connection={request.LocalPort}");
            }
        }

        // All criteria matched
        return (true, "All criteria matched");
    }

    /// <summary>
    /// Checks if a rule direction matches the connection direction.
    /// </summary>
    private static bool MatchesDirection(string? ruleDirection, string connectionDirection)
    {
        if (string.IsNullOrEmpty(ruleDirection))
            return true;

        // "both" matches any direction
        if (string.Equals(ruleDirection, RuleDirection.Both, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(ruleDirection, connectionDirection, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a rule protocol matches the connection protocol.
    /// </summary>
    private static bool MatchesProtocol(string? ruleProtocol, string connectionProtocol)
    {
        if (string.IsNullOrEmpty(ruleProtocol))
            return true;

        // "any" matches any protocol
        if (string.Equals(ruleProtocol, RuleProtocol.Any, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(ruleProtocol, connectionProtocol, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a process path matches the rule's process specification.
    /// </summary>
    private static bool MatchesProcess(string ruleProcess, string connectionProcess)
    {
        // Normalize paths for comparison
        var ruleNormalized = NormalizePath(ruleProcess);
        var connectionNormalized = NormalizePath(connectionProcess);

        // Exact match
        if (string.Equals(ruleNormalized, connectionNormalized, StringComparison.OrdinalIgnoreCase))
            return true;

        // Rule might be just the executable name (e.g., "chrome.exe")
        if (!ruleProcess.Contains('\\') && !ruleProcess.Contains('/'))
        {
            var connectionExeName = Path.GetFileName(connectionProcess);
            return string.Equals(ruleProcess, connectionExeName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    /// <summary>
    /// Normalizes a file path for comparison.
    /// </summary>
    private static string NormalizePath(string path)
    {
        return path.Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();
    }

    /// <summary>
    /// Checks if an IP address matches a rule's IP/CIDR specification.
    /// </summary>
    private static bool MatchesIpCidr(string ruleIpCidr, string connectionIp)
    {
        // Parse the connection IP
        if (!IPAddress.TryParse(connectionIp, out var connIp))
            return false;

        // Check if rule has CIDR notation
        var parts = ruleIpCidr.Split('/');
        if (!IPAddress.TryParse(parts[0], out var ruleIp))
            return false;

        // If no CIDR prefix, do exact match
        if (parts.Length == 1)
        {
            return ruleIp.Equals(connIp);
        }

        // Parse CIDR prefix
        if (!int.TryParse(parts[1], out var prefixLength))
            return false;

        // Check if addresses are same family
        if (ruleIp.AddressFamily != connIp.AddressFamily)
            return false;

        // Compare the network portions
        return IsInSubnet(connIp, ruleIp, prefixLength);
    }

    /// <summary>
    /// Checks if an IP is within a subnet.
    /// </summary>
    private static bool IsInSubnet(IPAddress address, IPAddress subnetAddress, int prefixLength)
    {
        var addressBytes = address.GetAddressBytes();
        var subnetBytes = subnetAddress.GetAddressBytes();

        if (addressBytes.Length != subnetBytes.Length)
            return false;

        var bitLength = addressBytes.Length * 8;
        if (prefixLength > bitLength)
            return false;

        var fullBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        // Compare full bytes
        for (int i = 0; i < fullBytes; i++)
        {
            if (addressBytes[i] != subnetBytes[i])
                return false;
        }

        // Compare remaining bits
        if (remainingBits > 0 && fullBytes < addressBytes.Length)
        {
            var mask = (byte)(0xFF << (8 - remainingBits));
            if ((addressBytes[fullBytes] & mask) != (subnetBytes[fullBytes] & mask))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a port matches a rule's port specification.
    /// </summary>
    private static bool MatchesPorts(string rulePorts, int connectionPort)
    {
        if (string.IsNullOrWhiteSpace(rulePorts))
            return true;

        // Parse the port specification (could be single port, range, or comma-separated)
        if (NetworkUtils.TryParsePorts(rulePorts, out var ranges))
        {
            foreach (var (start, end) in ranges)
            {
                if (connectionPort >= start && connectionPort <= end)
                    return true;
            }
        }

        return false;
    }
}
