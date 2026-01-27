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
}
