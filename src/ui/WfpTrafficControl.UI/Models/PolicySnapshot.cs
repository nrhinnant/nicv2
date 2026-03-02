namespace WfpTrafficControl.UI.Models;

/// <summary>
/// Represents a snapshot of the policy state for undo/redo functionality.
/// </summary>
public class PolicySnapshot
{
    public required string PolicyVersion { get; init; }
    public required string DefaultAction { get; init; }
    public required List<RuleSnapshot> Rules { get; init; }
    public required string Description { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Represents a snapshot of a single rule for undo/redo functionality.
/// </summary>
public class RuleSnapshot
{
    public required string Id { get; init; }
    public required string Action { get; init; }
    public required string Direction { get; init; }
    public required string Protocol { get; init; }
    public required string Process { get; init; }
    public required string RemoteIp { get; init; }
    public required string RemotePorts { get; init; }
    public required string LocalIp { get; init; }
    public required string LocalPorts { get; init; }
    public required int Priority { get; init; }
    public required bool Enabled { get; init; }
    public required string Comment { get; init; }
}
