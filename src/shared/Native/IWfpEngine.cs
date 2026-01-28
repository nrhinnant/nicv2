using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Shared.Native;

/// <summary>
/// Interface for WFP engine operations.
/// Enables unit testing with mock implementations.
/// </summary>
public interface IWfpEngine
{
    /// <summary>
    /// Ensures the WFP provider and sublayer exist, creating them if necessary.
    /// This operation is idempotent — calling it multiple times has the same effect as calling it once.
    /// </summary>
    /// <returns>
    /// A Result indicating success or failure.
    /// On failure, the WFP state is unchanged (transaction aborted).
    /// </returns>
    Result EnsureProviderAndSublayerExist();

    /// <summary>
    /// Removes the WFP provider and sublayer if they exist.
    /// This operation is idempotent — calling it multiple times has the same effect as calling it once.
    /// </summary>
    /// <returns>
    /// A Result indicating success or failure.
    /// If the provider or sublayer are already removed, this returns success.
    /// Fails if the sublayer has filters (FWP_E_IN_USE).
    /// </returns>
    /// <remarks>
    /// This is the "panic rollback" mechanism. It can be called at any time to
    /// remove all WFP objects created by this service and restore normal connectivity.
    ///
    /// Important: If filters exist in the sublayer, deletion will fail with FWP_E_IN_USE.
    /// Filters must be removed before the sublayer can be deleted.
    /// </remarks>
    Result RemoveProviderAndSublayer();

    /// <summary>
    /// Checks if the WFP provider exists.
    /// </summary>
    /// <returns>A Result containing true if the provider exists, false if not found.</returns>
    Result<bool> ProviderExists();

    /// <summary>
    /// Checks if the WFP sublayer exists.
    /// </summary>
    /// <returns>A Result containing true if the sublayer exists, false if not found.</returns>
    Result<bool> SublayerExists();

    // ========================================
    // Demo Block Filter Operations
    // ========================================

    /// <summary>
    /// Adds the demo block filter that blocks outbound TCP to 1.1.1.1:443.
    /// This operation is idempotent — calling it when the filter already exists returns success.
    /// </summary>
    /// <returns>
    /// A Result indicating success or failure.
    /// On success, the demo filter is active in our sublayer.
    /// </returns>
    /// <remarks>
    /// Prerequisites: Provider and sublayer must exist (call EnsureProviderAndSublayerExist first).
    /// </remarks>
    Result AddDemoBlockFilter();

    /// <summary>
    /// Removes the demo block filter if it exists.
    /// This operation is idempotent — calling it when the filter doesn't exist returns success.
    /// </summary>
    /// <returns>A Result indicating success or failure.</returns>
    Result RemoveDemoBlockFilter();

    /// <summary>
    /// Checks if the demo block filter exists.
    /// </summary>
    /// <returns>A Result containing true if the filter exists, false if not found.</returns>
    Result<bool> DemoBlockFilterExists();

    /// <summary>
    /// Removes all filters in our sublayer using enumeration.
    /// This is the panic rollback mechanism.
    /// </summary>
    /// <returns>
    /// A Result containing the number of filters removed on success.
    /// On failure, returns an error without partial cleanup.
    /// </returns>
    /// <remarks>
    /// This enumerates all filters in our sublayer and deletes them in a transaction.
    /// Provider and sublayer are kept intact - call RemoveProviderAndSublayer for full teardown.
    /// This method is idempotent: calling it when no filters exist returns success with count 0.
    /// </remarks>
    Result<int> RemoveAllFilters();

    // ========================================
    // Policy Application Operations
    // ========================================

    /// <summary>
    /// Applies compiled filters to WFP, replacing all existing filters.
    /// </summary>
    /// <param name="filters">List of compiled filters to apply.</param>
    /// <returns>
    /// A Result containing the number of filters created on success.
    /// On failure, the WFP state is unchanged (transaction aborted).
    /// </returns>
    /// <remarks>
    /// This method:
    /// 1. Ensures provider and sublayer exist
    /// 2. Removes all existing filters in our sublayer
    /// 3. Adds all new filters from the compiled list
    /// 4. Commits atomically (all or nothing)
    /// </remarks>
    Result<ApplyResult> ApplyFilters(List<Policy.CompiledFilter> filters);
}

/// <summary>
/// Result of applying filters to WFP.
/// </summary>
public sealed class ApplyResult
{
    /// <summary>
    /// Number of filters successfully created.
    /// </summary>
    public int FiltersCreated { get; set; }

    /// <summary>
    /// Number of filters removed (from previous policy).
    /// </summary>
    public int FiltersRemoved { get; set; }

    /// <summary>
    /// Number of filters that were unchanged (already existed with same GUID).
    /// </summary>
    public int FiltersUnchanged { get; set; }

    /// <summary>
    /// Total number of filters now active.
    /// </summary>
    public int TotalActive => FiltersUnchanged + FiltersCreated;
}
