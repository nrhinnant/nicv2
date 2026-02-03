// src/shared/Native/IWfpInterop.cs
// Low-level WFP interop interface for testability
// Phase 19: WFP Mocking Refactor

using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Shared.Native;

/// <summary>
/// Low-level interface for WFP interop operations.
/// This interface abstracts the native P/Invoke calls to enable unit testing
/// of the higher-level WfpEngine logic without requiring actual WFP access.
/// </summary>
/// <remarks>
/// All methods operate on an engine handle obtained from <see cref="OpenEngine"/>.
/// The caller is responsible for managing the engine handle lifecycle.
/// Transaction management is handled separately via <see cref="IWfpNativeTransaction"/>.
/// </remarks>
public interface IWfpInterop
{
    // ========================================
    // Engine Session Management
    // ========================================

    /// <summary>
    /// Opens a session to the local WFP filter engine.
    /// </summary>
    /// <returns>A Result containing the engine handle on success.</returns>
    Result<WfpEngineHandle> OpenEngine();

    // ========================================
    // Provider Operations
    // ========================================

    /// <summary>
    /// Checks if our provider exists in WFP.
    /// </summary>
    /// <param name="engineHandle">Open WFP engine handle.</param>
    /// <returns>True if provider exists, false if not found.</returns>
    Result<bool> ProviderExists(IntPtr engineHandle);

    /// <summary>
    /// Adds our provider to WFP.
    /// </summary>
    /// <param name="engineHandle">Open WFP engine handle.</param>
    /// <returns>Success or error. Returns success if already exists (idempotent).</returns>
    Result AddProvider(IntPtr engineHandle);

    /// <summary>
    /// Deletes our provider from WFP.
    /// </summary>
    /// <param name="engineHandle">Open WFP engine handle.</param>
    /// <returns>Success or error. Returns success if not found (idempotent).</returns>
    Result DeleteProvider(IntPtr engineHandle);

    // ========================================
    // Sublayer Operations
    // ========================================

    /// <summary>
    /// Checks if our sublayer exists in WFP.
    /// </summary>
    /// <param name="engineHandle">Open WFP engine handle.</param>
    /// <returns>True if sublayer exists, false if not found.</returns>
    Result<bool> SublayerExists(IntPtr engineHandle);

    /// <summary>
    /// Adds our sublayer to WFP.
    /// </summary>
    /// <param name="engineHandle">Open WFP engine handle.</param>
    /// <returns>Success or error. Returns success if already exists (idempotent).</returns>
    Result AddSublayer(IntPtr engineHandle);

    /// <summary>
    /// Deletes our sublayer from WFP.
    /// </summary>
    /// <param name="engineHandle">Open WFP engine handle.</param>
    /// <returns>
    /// Success or error. Returns success if not found (idempotent).
    /// Fails with FWP_E_IN_USE if filters still exist in the sublayer.
    /// </returns>
    Result DeleteSublayer(IntPtr engineHandle);

    // ========================================
    // Filter Operations
    // ========================================

    /// <summary>
    /// Enumerates all filters in our sublayer.
    /// </summary>
    /// <param name="engineHandle">Open WFP engine handle.</param>
    /// <returns>List of existing filters with their GUIDs, IDs, and display names.</returns>
    Result<List<ExistingFilter>> EnumerateFiltersInSublayer(IntPtr engineHandle);

    /// <summary>
    /// Adds a filter to WFP from a compiled filter definition.
    /// </summary>
    /// <param name="engineHandle">Open WFP engine handle.</param>
    /// <param name="filter">The compiled filter to add.</param>
    /// <returns>The runtime filter ID on success.</returns>
    Result<ulong> AddFilter(IntPtr engineHandle, CompiledFilter filter);

    /// <summary>
    /// Deletes a filter by its GUID key.
    /// </summary>
    /// <param name="engineHandle">Open WFP engine handle.</param>
    /// <param name="filterKey">The filter's GUID key.</param>
    /// <returns>Success or error. Returns success if not found (idempotent).</returns>
    Result DeleteFilterByKey(IntPtr engineHandle, Guid filterKey);

    /// <summary>
    /// Deletes a filter by its runtime ID.
    /// </summary>
    /// <param name="engineHandle">Open WFP engine handle.</param>
    /// <param name="filterId">The filter's runtime ID.</param>
    /// <returns>Success or error. Returns success if not found (idempotent).</returns>
    Result DeleteFilterById(IntPtr engineHandle, ulong filterId);

    /// <summary>
    /// Checks if a filter exists by its GUID key.
    /// </summary>
    /// <param name="engineHandle">Open WFP engine handle.</param>
    /// <param name="filterKey">The filter's GUID key.</param>
    /// <returns>True if filter exists, false if not found.</returns>
    Result<bool> FilterExists(IntPtr engineHandle, Guid filterKey);
}
