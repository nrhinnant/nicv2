using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.UI.Models;

/// <summary>
/// Represents a pre-configured policy template that users can load.
/// </summary>
public class PolicyTemplate
{
    /// <summary>
    /// Unique identifier for the template.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name of the template.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Detailed description of what this template does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Category for grouping templates (e.g., "Security", "Privacy", "Development").
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Warning text to display to the user before applying (for potentially disruptive templates).
    /// </summary>
    public string? Warning { get; init; }

    /// <summary>
    /// Factory function to create the policy. Uses a factory to avoid holding
    /// all policy objects in memory and to ensure fresh timestamps.
    /// </summary>
    public required Func<Policy> CreatePolicy { get; init; }
}
