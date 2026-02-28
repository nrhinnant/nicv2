// src/shared/Policy/FilterDiff.cs
// Idempotent reconciliation: diff computation between desired and current WFP filters
// Phase 13: Idempotent Reconcile

namespace WfpTrafficControl.Shared.Policy;

/// <summary>
/// Represents a filter currently installed in WFP.
/// Contains the minimal metadata needed for reconciliation.
/// </summary>
public sealed record ExistingFilter
{
    /// <summary>
    /// The filter's GUID key (used for comparison and deletion).
    /// </summary>
    public Guid FilterKey { get; init; }

    /// <summary>
    /// The filter's runtime ID (assigned by WFP, used for deletion by ID).
    /// </summary>
    public ulong FilterId { get; init; }

    /// <summary>
    /// Display name of the filter (for logging/debugging).
    /// </summary>
    public string? DisplayName { get; init; }
}

/// <summary>
/// Result of computing the difference between desired and current filter state.
/// </summary>
public sealed class FilterDiff
{
    /// <summary>
    /// Filters that need to be added (exist in desired but not in current).
    /// </summary>
    public List<CompiledFilter> ToAdd { get; } = new();

    /// <summary>
    /// Filter GUIDs that need to be removed (exist in current but not in desired).
    /// </summary>
    public List<Guid> ToRemove { get; } = new();

    /// <summary>
    /// Number of filters that already exist and need no changes.
    /// </summary>
    public int Unchanged { get; set; }

    /// <summary>
    /// True if there are no changes to make (all filters already match).
    /// </summary>
    public bool IsEmpty => ToAdd.Count == 0 && ToRemove.Count == 0;

    /// <summary>
    /// True if there are changes to make.
    /// </summary>
    public bool HasChanges => !IsEmpty;

    /// <summary>
    /// Total number of filters that will be in WFP after applying the diff.
    /// </summary>
    public int FinalCount => Unchanged + ToAdd.Count;
}

/// <summary>
/// Computes the difference between desired and current WFP filter state.
/// </summary>
public static class FilterDiffComputer
{
    /// <summary>
    /// Computes the diff between desired filter state and current WFP state.
    /// </summary>
    /// <param name="desired">The filters we want to have installed.</param>
    /// <param name="current">The filters currently installed in WFP.</param>
    /// <returns>A diff describing what needs to be added and removed.</returns>
    /// <remarks>
    /// Comparison is based on FilterKey (GUID) only.
    /// Filter GUIDs are derived from rule ID, port index, AND rule content
    /// (action, protocol, direction, IP, ports, process). This means if rule
    /// content changes, the GUID changes, and the diff will correctly identify
    /// the old filter for removal and the new filter for addition.
    /// </remarks>
    public static FilterDiff ComputeDiff(
        IReadOnlyList<CompiledFilter>? desired,
        IReadOnlyList<ExistingFilter>? current)
    {
        var diff = new FilterDiff();

        // Handle null/empty cases
        var desiredFilters = desired ?? Array.Empty<CompiledFilter>();
        var currentFilters = current ?? Array.Empty<ExistingFilter>();

        // Build a set of current filter GUIDs for O(1) lookup
        // Pre-allocate capacity and iterate directly to avoid LINQ iterator allocation
        var currentGuids = new HashSet<Guid>(currentFilters.Count);
        foreach (var f in currentFilters)
        {
            currentGuids.Add(f.FilterKey);
        }

        // Build a set of desired filter GUIDs for O(1) lookup
        var desiredGuids = new HashSet<Guid>(desiredFilters.Count);
        foreach (var f in desiredFilters)
        {
            desiredGuids.Add(f.FilterKey);
        }

        // Find filters to add: in desired but not in current
        foreach (var filter in desiredFilters)
        {
            if (!currentGuids.Contains(filter.FilterKey))
            {
                diff.ToAdd.Add(filter);
            }
            else
            {
                // Filter already exists
                diff.Unchanged++;
            }
        }

        // Find filters to remove: in current but not in desired
        foreach (var filter in currentFilters)
        {
            if (!desiredGuids.Contains(filter.FilterKey))
            {
                diff.ToRemove.Add(filter.FilterKey);
            }
        }

        return diff;
    }
}
