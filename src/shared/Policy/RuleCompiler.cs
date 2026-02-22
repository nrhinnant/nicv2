// src/shared/Policy/RuleCompiler.cs
// Compiles policy rules to WFP filter definitions
// Phase 12: Compile Outbound TCP Rules
// Phase 15: Added Inbound TCP Rule Support

using System.Net;
using WfpTrafficControl.Shared.Native;

namespace WfpTrafficControl.Shared.Policy;

/// <summary>
/// Represents a compiled WFP filter ready for creation.
/// Contains all data needed to call FwpmFilterAdd0.
/// </summary>
public sealed class CompiledFilter
{
    /// <summary>
    /// Unique GUID for this filter (derived from rule ID).
    /// </summary>
    public Guid FilterKey { get; set; }

    /// <summary>
    /// Display name for the filter.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description for the filter.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The action (block or allow).
    /// </summary>
    public FilterAction Action { get; set; }

    /// <summary>
    /// Weight/priority of the filter (higher = higher priority).
    /// </summary>
    public ulong Weight { get; set; }

    /// <summary>
    /// The rule ID this filter was compiled from.
    /// </summary>
    public string RuleId { get; set; } = string.Empty;

    /// <summary>
    /// Protocol number (6 = TCP).
    /// </summary>
    public byte Protocol { get; set; } = 6; // TCP

    /// <summary>
    /// Traffic direction: "inbound" or "outbound".
    /// Determines which WFP layer the filter is added to.
    /// </summary>
    public string Direction { get; set; } = RuleDirection.Outbound;

    /// <summary>
    /// Remote IP address (host byte order).
    /// </summary>
    public uint? RemoteIpAddress { get; set; }

    /// <summary>
    /// Remote IP subnet mask (host byte order). 0xFFFFFFFF for exact match.
    /// </summary>
    public uint RemoteIpMask { get; set; } = 0xFFFFFFFF;

    /// <summary>
    /// Remote port (single port match). Null if using range.
    /// </summary>
    public ushort? RemotePort { get; set; }

    /// <summary>
    /// Remote port range start (inclusive). Used when RemotePort is null.
    /// </summary>
    public ushort? RemotePortRangeStart { get; set; }

    /// <summary>
    /// Remote port range end (inclusive). Used when RemotePort is null.
    /// </summary>
    public ushort? RemotePortRangeEnd { get; set; }

    /// <summary>
    /// Process path to match (full DOS path). Null for any process.
    /// </summary>
    public string? ProcessPath { get; set; }
}

/// <summary>
/// Filter action type.
/// </summary>
public enum FilterAction
{
    Block,
    Allow
}

/// <summary>
/// Result of compiling a policy to WFP filters.
/// </summary>
public sealed class CompilationResult
{
    /// <summary>
    /// Whether compilation succeeded.
    /// </summary>
    public bool IsSuccess => Errors.Count == 0;

    /// <summary>
    /// Compiled filters ready for WFP.
    /// </summary>
    public List<CompiledFilter> Filters { get; } = new();

    /// <summary>
    /// Compilation errors (unsupported features, invalid rules).
    /// </summary>
    public List<CompilationError> Errors { get; } = new();

    /// <summary>
    /// Warnings (rules skipped due to being disabled, etc.).
    /// </summary>
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// Number of rules that were skipped (disabled rules).
    /// </summary>
    public int SkippedRules { get; set; }

    /// <summary>
    /// Adds an error.
    /// </summary>
    public void AddError(string ruleId, string message)
    {
        Errors.Add(new CompilationError(ruleId, message));
    }

    /// <summary>
    /// Adds a warning.
    /// </summary>
    public void AddWarning(string message)
    {
        Warnings.Add(message);
    }
}

/// <summary>
/// A compilation error for a specific rule.
/// </summary>
public sealed record CompilationError(string RuleId, string Message);

/// <summary>
/// Compiles policy rules to WFP filter definitions.
///
/// Phase 12 Scope (Outbound TCP only):
/// - Supports: direction=outbound, protocol=tcp
/// - Supports: remote.ip (IPv4 only), remote.ports (single or range)
/// - Supports: process (full path)
/// - Supports: action (allow/block)
/// - Errors on: direction=inbound/both, protocol=udp/any, local endpoint, IPv6
///
/// Phase 15 Scope (Added Inbound TCP):
/// - Supports: direction=inbound (TCP only)
/// - Uses FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4 layer for inbound rules
/// - remote.ip and remote.ports match the connecting client
/// - process matches the listening application accepting the connection
/// - Errors on: direction=both, protocol=udp/any
///
/// Phase 16 Scope (Added Outbound UDP):
/// - Supports: direction=outbound, protocol=udp
/// - Uses same FWPM_LAYER_ALE_AUTH_CONNECT_V4 layer as outbound TCP
/// - Supports: remote.ip (IPv4 only), remote.ports (single or range)
/// - Supports: process (full path)
/// - Errors on: direction=inbound + protocol=udp, protocol=any
/// </summary>
public static class RuleCompiler
{
    /// <summary>
    /// Base weight for policy filters. Rules with higher priority get higher weight.
    /// </summary>
    private const ulong BaseWeight = 1000;

    /// <summary>
    /// Compiles a policy to WFP filter definitions.
    /// Only enabled rules with supported features are compiled.
    /// </summary>
    /// <param name="policy">The policy to compile.</param>
    /// <returns>Compilation result with filters and any errors.</returns>
    public static CompilationResult Compile(Policy policy)
    {
        var result = new CompilationResult();

        if (policy == null)
        {
            result.AddError("(policy)", "Policy cannot be null");
            return result;
        }

        if (policy.Rules == null || policy.Rules.Count == 0)
        {
            result.AddWarning("Policy has no rules");
            return result;
        }

        foreach (var rule in policy.Rules)
        {
            CompileRule(rule, result);
        }

        return result;
    }

    /// <summary>
    /// Compiles a single rule to one or more WFP filter definitions.
    /// </summary>
    private static void CompileRule(Rule rule, CompilationResult result)
    {
        var ruleId = rule.Id ?? "(unknown)";

        // Skip disabled rules
        if (!rule.Enabled)
        {
            result.SkippedRules++;
            result.AddWarning($"Rule '{ruleId}' is disabled, skipping");
            return;
        }

        // Validate supported features
        if (!ValidateRuleSupport(rule, result))
        {
            return; // Errors already added
        }

        // Parse remote endpoint
        uint? remoteIp = null;
        uint remoteMask = 0xFFFFFFFF;

        if (rule.Remote?.Ip != null)
        {
            if (!TryParseIpv4Cidr(rule.Remote.Ip, out remoteIp, out remoteMask))
            {
                result.AddError(ruleId, $"Failed to parse remote IP: {rule.Remote.Ip}");
                return;
            }
        }

        // Parse ports - may create multiple filters for comma-separated ports
        var portSpecs = ParsePortSpecs(rule.Remote?.Ports);

        // Determine the normalized direction
        var direction = string.Equals(rule.Direction, RuleDirection.Inbound, StringComparison.OrdinalIgnoreCase)
            ? RuleDirection.Inbound
            : RuleDirection.Outbound;

        if (portSpecs.Count == 0)
        {
            // No port specified - create single filter matching any port
            var filter = CreateFilter(rule, ruleId, remoteIp, remoteMask, null, null, null, 0, direction);
            result.Filters.Add(filter);
        }
        else
        {
            // Create one filter per port specification
            int portIndex = 0;
            foreach (var (start, end) in portSpecs)
            {
                CompiledFilter filter;
                if (start == end)
                {
                    // Single port
                    filter = CreateFilter(rule, ruleId, remoteIp, remoteMask, (ushort)start, null, null, portIndex, direction);
                }
                else
                {
                    // Port range
                    filter = CreateFilter(rule, ruleId, remoteIp, remoteMask, null, (ushort)start, (ushort)end, portIndex, direction);
                }
                result.Filters.Add(filter);
                portIndex++;
            }
        }
    }

    /// <summary>
    /// Validates that a rule uses only supported features.
    /// </summary>
    private static bool ValidateRuleSupport(Rule rule, CompilationResult result)
    {
        var ruleId = rule.Id ?? "(unknown)";
        var hasErrors = false;

        // Check direction - outbound and inbound supported, "both" not supported
        var isOutbound = string.Equals(rule.Direction, RuleDirection.Outbound, StringComparison.OrdinalIgnoreCase);
        var isInbound = string.Equals(rule.Direction, RuleDirection.Inbound, StringComparison.OrdinalIgnoreCase);
        if (!isOutbound && !isInbound)
        {
            result.AddError(ruleId, $"Unsupported direction: '{rule.Direction}'. Only 'outbound' and 'inbound' are supported in this version.");
            hasErrors = true;
        }

        // Check protocol - TCP supported for both directions, UDP only for outbound
        var isTcp = string.Equals(rule.Protocol, RuleProtocol.Tcp, StringComparison.OrdinalIgnoreCase);
        var isUdp = string.Equals(rule.Protocol, RuleProtocol.Udp, StringComparison.OrdinalIgnoreCase);

        if (!isTcp && !isUdp)
        {
            result.AddError(ruleId, $"Unsupported protocol: '{rule.Protocol}'. Only 'tcp' and 'udp' are supported in this version.");
            hasErrors = true;
        }
        else if (isUdp && isInbound)
        {
            result.AddError(ruleId, "Inbound UDP rules are not supported in this version. Use protocol 'tcp' for inbound rules.");
            hasErrors = true;
        }

        // Check for unsupported local endpoint
        if (rule.Local != null && (!string.IsNullOrEmpty(rule.Local.Ip) || !string.IsNullOrEmpty(rule.Local.Ports)))
        {
            result.AddError(ruleId, "Local endpoint filters are not supported in this version.");
            hasErrors = true;
        }

        // Check for IPv6 in remote IP
        if (!string.IsNullOrEmpty(rule.Remote?.Ip))
        {
            if (rule.Remote.Ip.Contains(':'))
            {
                result.AddError(ruleId, "IPv6 addresses are not supported in this version. Use IPv4 only.");
                hasErrors = true;
            }
        }

        // Validate action
        if (!RuleAction.IsValid(rule.Action))
        {
            result.AddError(ruleId, $"Invalid action: '{rule.Action}'");
            hasErrors = true;
        }

        return !hasErrors;
    }

    /// <summary>
    /// Gets the protocol byte for a given protocol string.
    /// </summary>
    private static byte GetProtocolByte(string protocol)
    {
        if (string.Equals(protocol, RuleProtocol.Udp, StringComparison.OrdinalIgnoreCase))
        {
            return WfpConstants.ProtocolUdp;
        }
        // Default to TCP for any other value (already validated by ValidateRuleSupport)
        return WfpConstants.ProtocolTcp;
    }

    /// <summary>
    /// Creates a compiled filter from rule data.
    /// </summary>
    private static CompiledFilter CreateFilter(
        Rule rule,
        string ruleId,
        uint? remoteIp,
        uint remoteMask,
        ushort? singlePort,
        ushort? rangeStart,
        ushort? rangeEnd,
        int portIndex,
        string direction)
    {
        // Generate deterministic filter GUID from rule ID, port index, AND content
        // This ensures that if rule content changes, the GUID changes, causing the diff
        // to correctly identify the filter as needing removal and re-addition.
        var filterKey = GenerateFilterGuid(
            ruleId, portIndex, rule.Action, rule.Protocol, direction,
            remoteIp, remoteMask, singlePort, rangeStart, rangeEnd, rule.Process);

        // Build display name
        var displayName = $"WfpTrafficControl: {ruleId}";
        if (portIndex > 0)
        {
            displayName += $" (port-{portIndex + 1})";
        }

        // Build description
        var description = $"Compiled from rule '{ruleId}': {rule.Action} {rule.Protocol} {direction}";
        if (remoteIp.HasValue)
        {
            var ipStr = IpToString(remoteIp.Value);
            if (remoteMask != 0xFFFFFFFF)
            {
                var prefixLen = MaskToPrefixLength(remoteMask);
                description += $" to {ipStr}/{prefixLen}";
            }
            else
            {
                description += $" to {ipStr}";
            }
        }
        if (singlePort.HasValue)
        {
            description += $":{singlePort.Value}";
        }
        else if (rangeStart.HasValue && rangeEnd.HasValue)
        {
            description += $":{rangeStart.Value}-{rangeEnd.Value}";
        }

        // Calculate weight from priority (higher priority = higher weight)
        var weight = BaseWeight + (ulong)Math.Max(0, rule.Priority);

        return new CompiledFilter
        {
            FilterKey = filterKey,
            DisplayName = displayName,
            Description = description,
            Action = string.Equals(rule.Action, RuleAction.Block, StringComparison.OrdinalIgnoreCase)
                ? FilterAction.Block
                : FilterAction.Allow,
            Weight = weight,
            RuleId = ruleId,
            Protocol = GetProtocolByte(rule.Protocol),
            Direction = direction,
            RemoteIpAddress = remoteIp,
            RemoteIpMask = remoteMask,
            RemotePort = singlePort,
            RemotePortRangeStart = rangeStart,
            RemotePortRangeEnd = rangeEnd,
            ProcessPath = rule.Process
        };
    }

    /// <summary>
    /// Generates a deterministic GUID from rule ID, port index, and content fields.
    /// Including content fields ensures that rule modifications (e.g., changing action
    /// from allow to block) produce a different GUID, triggering proper diff detection.
    /// </summary>
    private static Guid GenerateFilterGuid(
        string ruleId,
        int portIndex,
        string action,
        string protocol,
        string direction,
        uint? remoteIp,
        uint remoteMask,
        ushort? singlePort,
        ushort? rangeStart,
        ushort? rangeEnd,
        string? processPath)
    {
        // Build a deterministic string from all content-relevant fields
        // Format: ruleId:portIndex|action|protocol|direction|ip/mask|port|process
        var sb = new System.Text.StringBuilder();
        sb.Append(ruleId);
        sb.Append(':');
        sb.Append(portIndex);
        sb.Append('|');
        sb.Append(action ?? string.Empty);
        sb.Append('|');
        sb.Append(protocol ?? string.Empty);
        sb.Append('|');
        sb.Append(direction ?? string.Empty);
        sb.Append('|');
        if (remoteIp.HasValue)
        {
            sb.Append(remoteIp.Value);
            sb.Append('/');
            sb.Append(remoteMask);
        }
        sb.Append('|');
        if (singlePort.HasValue)
        {
            sb.Append(singlePort.Value);
        }
        else if (rangeStart.HasValue && rangeEnd.HasValue)
        {
            sb.Append(rangeStart.Value);
            sb.Append('-');
            sb.Append(rangeEnd.Value);
        }
        sb.Append('|');
        sb.Append(processPath ?? string.Empty);

        var input = sb.ToString();
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));

        // Set version 4 (random) and variant bits for valid GUID
        hash[6] = (byte)((hash[6] & 0x0F) | 0x40); // Version 4
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // Variant 1

        return new Guid(hash);
    }

    /// <summary>
    /// Parses an IPv4 address or CIDR to host byte order values.
    /// </summary>
    private static bool TryParseIpv4Cidr(string ipOrCidr, out uint? ip, out uint mask)
    {
        ip = null;
        mask = 0xFFFFFFFF;

        if (string.IsNullOrWhiteSpace(ipOrCidr))
            return false;

        var trimmed = ipOrCidr.Trim();
        int prefixLength = 32;

        // Check for CIDR notation
        if (trimmed.Contains('/'))
        {
            var parts = trimmed.Split('/', 2);
            trimmed = parts[0];
            if (!int.TryParse(parts[1], out prefixLength) || prefixLength < 0 || prefixLength > 32)
                return false;
        }

        // Parse IP address
        if (!IPAddress.TryParse(trimmed, out var ipAddr))
            return false;

        // Must be IPv4
        if (ipAddr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;

        // Convert to host byte order (WFP uses host byte order)
        var bytes = ipAddr.GetAddressBytes();
        ip = (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);

        // Calculate mask from prefix length
        if (prefixLength == 0)
        {
            mask = 0;
        }
        else if (prefixLength == 32)
        {
            mask = 0xFFFFFFFF;
        }
        else
        {
            mask = 0xFFFFFFFF << (32 - prefixLength);
        }

        return true;
    }

    /// <summary>
    /// Parses port specification into list of (start, end) ranges.
    /// </summary>
    private static List<(int Start, int End)> ParsePortSpecs(string? ports)
    {
        var ranges = new List<(int, int)>();

        if (string.IsNullOrWhiteSpace(ports))
            return ranges;

        // Use existing NetworkUtils parser
        if (NetworkUtils.TryParsePorts(ports, out var parsed))
        {
            ranges.AddRange(parsed);
        }

        return ranges;
    }

    /// <summary>
    /// Converts an IP in host byte order to string.
    /// </summary>
    private static string IpToString(uint ip)
    {
        return $"{(ip >> 24) & 0xFF}.{(ip >> 16) & 0xFF}.{(ip >> 8) & 0xFF}.{ip & 0xFF}";
    }

    /// <summary>
    /// Converts a subnet mask to CIDR prefix length.
    /// </summary>
    private static int MaskToPrefixLength(uint mask)
    {
        int count = 0;
        while ((mask & 0x80000000) != 0)
        {
            count++;
            mask <<= 1;
        }
        return count;
    }
}
