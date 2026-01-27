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
    /// Removes all filters in our sublayer.
    /// This is used for rollback to ensure the sublayer can be cleanly deleted if needed.
    /// </summary>
    /// <returns>A Result indicating success or failure.</returns>
    /// <remarks>
    /// This removes all filters we created but keeps the provider and sublayer intact.
    /// To completely clean up, call this followed by RemoveProviderAndSublayer.
    /// </remarks>
    Result RemoveAllFilters();
}
