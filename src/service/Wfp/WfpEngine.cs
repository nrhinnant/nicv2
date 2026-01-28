using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Native;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Service.Wfp;

/// <summary>
/// Implementation of WFP engine operations using the Windows Filtering Platform APIs.
/// All operations use GUIDs and display names from <see cref="WfpConstants"/>.
/// </summary>
public sealed class WfpEngine : IWfpEngine
{
    private readonly ILogger<WfpEngine> _logger;

    /// <summary>
    /// Sublayer weight. Higher values have higher priority.
    /// We use a moderate weight to avoid conflicts with system sublayers.
    /// </summary>
    private const ushort SublayerWeight = 0x8000; // 32768 - middle of the range

    public WfpEngine(ILogger<WfpEngine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Result EnsureProviderAndSublayerExist()
    {
        _logger.LogInformation("Ensuring WFP provider and sublayer exist");

        // Open engine session
        var openResult = WfpSession.OpenEngine();
        if (openResult.IsFailure)
        {
            _logger.LogError("Failed to open WFP engine: {Error}", openResult.Error);
            return Result.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        var engineHandle = handle.DangerousGetHandle();

        // Begin transaction for atomicity - auto-aborts on dispose if not committed
        _logger.LogDebug("Beginning WFP transaction");
        var txResult = WfpTransaction.Begin(engineHandle);
        if (txResult.IsFailure)
        {
            _logger.LogError("Failed to begin transaction: {Error}", txResult.Error);
            return Result.Failure(txResult.Error);
        }

        using var transaction = txResult.Value;

        try
        {
            // Step 1: Ensure provider exists
            var providerResult = EnsureProviderExistsInternal(engineHandle);
            if (providerResult.IsFailure)
            {
                _logger.LogError("Failed to ensure provider exists: {Error}", providerResult.Error);
                return providerResult; // Transaction aborted by dispose
            }

            // Step 2: Ensure sublayer exists
            var sublayerResult = EnsureSublayerExistsInternal(engineHandle);
            if (sublayerResult.IsFailure)
            {
                _logger.LogError("Failed to ensure sublayer exists: {Error}", sublayerResult.Error);
                return sublayerResult; // Transaction aborted by dispose
            }

            // Commit transaction
            _logger.LogDebug("Committing WFP transaction");
            var commitResult = transaction.Commit();
            if (commitResult.IsFailure)
            {
                _logger.LogError("Failed to commit transaction: {Error}", commitResult.Error);
                return commitResult;
            }

            _logger.LogInformation("WFP provider and sublayer are ready");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during bootstrap");
            // Transaction will be aborted by dispose
            return Result.Failure(ErrorCodes.WfpError, $"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result RemoveProviderAndSublayer()
    {
        _logger.LogInformation("Removing WFP provider and sublayer");

        // Open engine session
        var openResult = WfpSession.OpenEngine();
        if (openResult.IsFailure)
        {
            _logger.LogError("Failed to open WFP engine: {Error}", openResult.Error);
            return Result.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        var engineHandle = handle.DangerousGetHandle();

        // Begin transaction for atomicity - auto-aborts on dispose if not committed
        _logger.LogDebug("Beginning WFP transaction");
        var txResult = WfpTransaction.Begin(engineHandle);
        if (txResult.IsFailure)
        {
            _logger.LogError("Failed to begin transaction: {Error}", txResult.Error);
            return Result.Failure(txResult.Error);
        }

        using var transaction = txResult.Value;

        try
        {
            // Step 1: Remove sublayer first (must be done before provider)
            // If sublayer has filters, this will fail with FWP_E_IN_USE
            var sublayerResult = RemoveSublayerInternal(engineHandle);
            if (sublayerResult.IsFailure)
            {
                _logger.LogError("Failed to remove sublayer: {Error}", sublayerResult.Error);
                return sublayerResult; // Transaction aborted by dispose
            }

            // Step 2: Remove provider
            var providerResult = RemoveProviderInternal(engineHandle);
            if (providerResult.IsFailure)
            {
                _logger.LogError("Failed to remove provider: {Error}", providerResult.Error);
                return providerResult; // Transaction aborted by dispose
            }

            // Commit transaction
            _logger.LogDebug("Committing WFP transaction");
            var commitResult = transaction.Commit();
            if (commitResult.IsFailure)
            {
                _logger.LogError("Failed to commit transaction: {Error}", commitResult.Error);
                return commitResult;
            }

            _logger.LogInformation("WFP provider and sublayer removed successfully");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during teardown");
            // Transaction will be aborted by dispose
            return Result.Failure(ErrorCodes.WfpError, $"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result<bool> ProviderExists()
    {
        var openResult = WfpSession.OpenEngine();
        if (openResult.IsFailure)
        {
            return Result<bool>.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        return ProviderExistsInternal(handle.DangerousGetHandle());
    }

    /// <inheritdoc/>
    public Result<bool> SublayerExists()
    {
        var openResult = WfpSession.OpenEngine();
        if (openResult.IsFailure)
        {
            return Result<bool>.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        return SublayerExistsInternal(handle.DangerousGetHandle());
    }

    // ========================================
    // Internal Helper Methods
    // ========================================

    private Result<bool> ProviderExistsInternal(IntPtr engineHandle)
    {
        var providerGuid = WfpConstants.ProviderGuid;
        var result = NativeMethods.FwpmProviderGetByKey0(engineHandle, in providerGuid, out IntPtr providerPtr);

        // Check for "not found" - Windows may return different codes
        if (result == NativeMethods.FWP_E_PROVIDER_NOT_FOUND ||
            result == NativeMethods.FWP_E_PROVIDER_NOT_FOUND_ALT)
        {
            _logger.LogDebug("Provider does not exist (error code: 0x{ErrorCode:X8})", result);
            return Result<bool>.Success(false);
        }

        if (!WfpErrorTranslator.IsSuccess(result))
        {
            return WfpErrorTranslator.ToFailedResult<bool>(result, "Failed to check if provider exists");
        }

        // Free the returned memory
        if (providerPtr != IntPtr.Zero)
        {
            NativeMethods.FwpmFreeMemory0(ref providerPtr);
        }

        _logger.LogDebug("Provider exists");
        return Result<bool>.Success(true);
    }

    private Result<bool> SublayerExistsInternal(IntPtr engineHandle)
    {
        var sublayerGuid = WfpConstants.SublayerGuid;
        var result = NativeMethods.FwpmSubLayerGetByKey0(engineHandle, in sublayerGuid, out IntPtr sublayerPtr);

        // Check for "not found" - Windows may return different codes
        if (result == NativeMethods.FWP_E_SUBLAYER_NOT_FOUND ||
            result == NativeMethods.FWP_E_SUBLAYER_NOT_FOUND_ALT)
        {
            _logger.LogDebug("Sublayer does not exist (error code: 0x{ErrorCode:X8})", result);
            return Result<bool>.Success(false);
        }

        if (!WfpErrorTranslator.IsSuccess(result))
        {
            return WfpErrorTranslator.ToFailedResult<bool>(result, "Failed to check if sublayer exists");
        }

        // Free the returned memory
        if (sublayerPtr != IntPtr.Zero)
        {
            NativeMethods.FwpmFreeMemory0(ref sublayerPtr);
        }

        _logger.LogDebug("Sublayer exists");
        return Result<bool>.Success(true);
    }

    private Result EnsureProviderExistsInternal(IntPtr engineHandle)
    {
        // Check if already exists
        var existsResult = ProviderExistsInternal(engineHandle);
        if (existsResult.IsFailure)
        {
            return Result.Failure(existsResult.Error);
        }

        if (existsResult.Value)
        {
            _logger.LogDebug("Provider already exists, skipping creation");
            return Result.Success();
        }

        // Create the provider
        _logger.LogInformation("Creating WFP provider: {ProviderName} ({ProviderGuid})",
            WfpConstants.ProviderName, WfpConstants.ProviderGuid);

        var provider = new FWPM_PROVIDER0
        {
            providerKey = WfpConstants.ProviderGuid,
            displayData = new FWPM_DISPLAY_DATA0
            {
                name = WfpConstants.ProviderName,
                description = WfpConstants.ProviderDescription
            },
            flags = FwpmProviderFlags.FWPM_PROVIDER_FLAG_NONE,
            providerData = IntPtr.Zero,
            serviceName = IntPtr.Zero
        };

        var result = NativeMethods.FwpmProviderAdd0(engineHandle, in provider, IntPtr.Zero);

        // Handle "already exists" as success for idempotency
        if (result == NativeMethods.FWP_E_ALREADY_EXISTS)
        {
            _logger.LogDebug("Provider already exists (race condition), treating as success");
            return Result.Success();
        }

        if (!WfpErrorTranslator.IsSuccess(result))
        {
            return WfpErrorTranslator.ToFailedResult(result, "Failed to create WFP provider");
        }

        _logger.LogInformation("WFP provider created successfully");
        return Result.Success();
    }

    private Result EnsureSublayerExistsInternal(IntPtr engineHandle)
    {
        // Check if already exists
        var existsResult = SublayerExistsInternal(engineHandle);
        if (existsResult.IsFailure)
        {
            return Result.Failure(existsResult.Error);
        }

        if (existsResult.Value)
        {
            _logger.LogDebug("Sublayer already exists, skipping creation");
            return Result.Success();
        }

        // Create the sublayer
        _logger.LogInformation("Creating WFP sublayer: {SublayerName} ({SublayerGuid})",
            WfpConstants.SublayerName, WfpConstants.SublayerGuid);

        // We need to pin the provider GUID so we can pass a pointer to it
        var providerGuid = WfpConstants.ProviderGuid;
        var providerGuidHandle = GCHandle.Alloc(providerGuid, GCHandleType.Pinned);

        try
        {
            var sublayer = new FWPM_SUBLAYER0
            {
                subLayerKey = WfpConstants.SublayerGuid,
                displayData = new FWPM_DISPLAY_DATA0
                {
                    name = WfpConstants.SublayerName,
                    description = WfpConstants.SublayerDescription
                },
                flags = FwpmSublayerFlags.FWPM_SUBLAYER_FLAG_NONE,
                providerKey = providerGuidHandle.AddrOfPinnedObject(),
                providerData = IntPtr.Zero,
                weight = SublayerWeight
            };

            var result = NativeMethods.FwpmSubLayerAdd0(engineHandle, in sublayer, IntPtr.Zero);

            // Handle "already exists" as success for idempotency
            if (result == NativeMethods.FWP_E_ALREADY_EXISTS)
            {
                _logger.LogDebug("Sublayer already exists (race condition), treating as success");
                return Result.Success();
            }

            if (!WfpErrorTranslator.IsSuccess(result))
            {
                return WfpErrorTranslator.ToFailedResult(result, "Failed to create WFP sublayer");
            }

            _logger.LogInformation("WFP sublayer created successfully");
            return Result.Success();
        }
        finally
        {
            providerGuidHandle.Free();
        }
    }

    private Result RemoveProviderInternal(IntPtr engineHandle)
    {
        // Check if exists
        var existsResult = ProviderExistsInternal(engineHandle);
        if (existsResult.IsFailure)
        {
            return Result.Failure(existsResult.Error);
        }

        if (!existsResult.Value)
        {
            _logger.LogDebug("Provider does not exist, skipping removal");
            return Result.Success();
        }

        // Delete the provider
        _logger.LogInformation("Deleting WFP provider: {ProviderGuid}", WfpConstants.ProviderGuid);

        var providerGuid = WfpConstants.ProviderGuid;
        var result = NativeMethods.FwpmProviderDeleteByKey0(engineHandle, in providerGuid);

        // Handle "not found" as success for idempotency
        if (result == NativeMethods.FWP_E_PROVIDER_NOT_FOUND ||
            result == NativeMethods.FWP_E_PROVIDER_NOT_FOUND_ALT)
        {
            _logger.LogDebug("Provider not found (race condition), treating as success");
            return Result.Success();
        }

        if (!WfpErrorTranslator.IsSuccess(result))
        {
            return WfpErrorTranslator.ToFailedResult(result, "Failed to delete WFP provider");
        }

        _logger.LogInformation("WFP provider deleted successfully");
        return Result.Success();
    }

    private Result RemoveSublayerInternal(IntPtr engineHandle)
    {
        // Check if exists
        var existsResult = SublayerExistsInternal(engineHandle);
        if (existsResult.IsFailure)
        {
            return Result.Failure(existsResult.Error);
        }

        if (!existsResult.Value)
        {
            _logger.LogDebug("Sublayer does not exist, skipping removal");
            return Result.Success();
        }

        // Delete the sublayer
        _logger.LogInformation("Deleting WFP sublayer: {SublayerGuid}", WfpConstants.SublayerGuid);

        var sublayerGuid = WfpConstants.SublayerGuid;
        var result = NativeMethods.FwpmSubLayerDeleteByKey0(engineHandle, in sublayerGuid);

        // Handle "not found" as success for idempotency
        if (result == NativeMethods.FWP_E_SUBLAYER_NOT_FOUND ||
            result == NativeMethods.FWP_E_SUBLAYER_NOT_FOUND_ALT)
        {
            _logger.LogDebug("Sublayer not found (race condition), treating as success");
            return Result.Success();
        }

        // FWP_E_IN_USE means filters exist in the sublayer
        if (result == NativeMethods.FWP_E_IN_USE)
        {
            _logger.LogError("Cannot delete sublayer: filters still exist in the sublayer");
            return Result.Failure(ErrorCodes.WfpError,
                "Cannot remove sublayer: filters still exist. Remove all filters first.");
        }

        if (!WfpErrorTranslator.IsSuccess(result))
        {
            return WfpErrorTranslator.ToFailedResult(result, "Failed to delete WFP sublayer");
        }

        _logger.LogInformation("WFP sublayer deleted successfully");
        return Result.Success();
    }

    // ========================================
    // Demo Block Filter Methods
    // ========================================

    /// <inheritdoc/>
    public Result AddDemoBlockFilter()
    {
        _logger.LogInformation("Adding demo block filter (block TCP to 1.1.1.1:443)");

        // Open engine session
        var openResult = WfpSession.OpenEngine();
        if (openResult.IsFailure)
        {
            _logger.LogError("Failed to open WFP engine: {Error}", openResult.Error);
            return Result.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        var engineHandle = handle.DangerousGetHandle();

        // Begin transaction
        _logger.LogDebug("Beginning WFP transaction for demo filter");
        var txResult = WfpTransaction.Begin(engineHandle);
        if (txResult.IsFailure)
        {
            _logger.LogError("Failed to begin transaction: {Error}", txResult.Error);
            return Result.Failure(txResult.Error);
        }

        using var transaction = txResult.Value;

        try
        {
            // Check if already exists (idempotent)
            var existsResult = DemoBlockFilterExistsInternal(engineHandle);
            if (existsResult.IsFailure)
            {
                return Result.Failure(existsResult.Error);
            }

            if (existsResult.Value)
            {
                _logger.LogDebug("Demo block filter already exists, skipping creation");
                // Still commit the empty transaction for consistency
                transaction.Commit();
                return Result.Success();
            }

            // Create the filter
            var createResult = CreateDemoBlockFilterInternal(engineHandle);
            if (createResult.IsFailure)
            {
                _logger.LogError("Failed to create demo block filter: {Error}", createResult.Error);
                return Result.Failure(createResult.Error);
            }

            // Commit transaction
            var commitResult = transaction.Commit();
            if (commitResult.IsFailure)
            {
                _logger.LogError("Failed to commit transaction: {Error}", commitResult.Error);
                return commitResult;
            }

            _logger.LogInformation("Demo block filter created successfully (ID: {FilterId})", createResult.Value);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error adding demo block filter");
            return Result.Failure(ErrorCodes.WfpError, $"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result RemoveDemoBlockFilter()
    {
        _logger.LogInformation("Removing demo block filter");

        // Open engine session
        var openResult = WfpSession.OpenEngine();
        if (openResult.IsFailure)
        {
            _logger.LogError("Failed to open WFP engine: {Error}", openResult.Error);
            return Result.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        var engineHandle = handle.DangerousGetHandle();

        // Begin transaction
        _logger.LogDebug("Beginning WFP transaction for removing demo filter");
        var txResult = WfpTransaction.Begin(engineHandle);
        if (txResult.IsFailure)
        {
            _logger.LogError("Failed to begin transaction: {Error}", txResult.Error);
            return Result.Failure(txResult.Error);
        }

        using var transaction = txResult.Value;

        try
        {
            // Delete by GUID key directly (simpler than get-then-delete-by-id)
            var filterGuid = WfpConstants.DemoBlockFilterGuid;
            _logger.LogDebug("Deleting demo block filter by GUID: {FilterGuid}", filterGuid);
            var deleteResult = NativeMethods.FwpmFilterDeleteByKey0(engineHandle, in filterGuid);

            // Not found is success for idempotent delete
            if (deleteResult == NativeMethods.FWP_E_FILTER_NOT_FOUND)
            {
                _logger.LogDebug("Demo block filter does not exist, skipping removal");
                transaction.Commit();
                return Result.Success();
            }

            if (!WfpErrorTranslator.IsSuccess(deleteResult))
            {
                return WfpErrorTranslator.ToFailedResult(deleteResult, "Failed to delete demo block filter");
            }

            // Commit transaction
            var commitResult = transaction.Commit();
            if (commitResult.IsFailure)
            {
                _logger.LogError("Failed to commit transaction: {Error}", commitResult.Error);
                return commitResult;
            }

            _logger.LogInformation("Demo block filter removed successfully");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error removing demo block filter");
            return Result.Failure(ErrorCodes.WfpError, $"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result<bool> DemoBlockFilterExists()
    {
        var openResult = WfpSession.OpenEngine();
        if (openResult.IsFailure)
        {
            return Result<bool>.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        return DemoBlockFilterExistsInternal(handle.DangerousGetHandle());
    }

    /// <inheritdoc/>
    public Result<int> RemoveAllFilters()
    {
        _logger.LogInformation("Removing all filters from our sublayer (panic rollback)");

        // Open engine session
        var openResult = WfpSession.OpenEngine();
        if (openResult.IsFailure)
        {
            _logger.LogError("Failed to open WFP engine: {Error}", openResult.Error);
            return Result<int>.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        var engineHandle = handle.DangerousGetHandle();

        // Phase 1: Enumerate filters in our sublayer (outside transaction)
        var enumerateResult = EnumerateFiltersInOurSublayer(engineHandle);
        if (enumerateResult.IsFailure)
        {
            _logger.LogError("Failed to enumerate filters: {Error}", enumerateResult.Error);
            return Result<int>.Failure(enumerateResult.Error);
        }

        var existingFilters = enumerateResult.Value;
        if (existingFilters.Count == 0)
        {
            _logger.LogInformation("No filters found in our sublayer, nothing to remove");
            return Result<int>.Success(0);
        }

        _logger.LogInformation("Found {FilterCount} filter(s) in our sublayer to remove", existingFilters.Count);

        // Phase 2: Delete all filters in a transaction
        _logger.LogDebug("Beginning WFP transaction for filter deletion");
        var txResult = WfpTransaction.Begin(engineHandle);
        if (txResult.IsFailure)
        {
            _logger.LogError("Failed to begin transaction: {Error}", txResult.Error);
            return Result<int>.Failure(txResult.Error);
        }

        using var transaction = txResult.Value;

        try
        {
            int deletedCount = 0;
            foreach (var filter in existingFilters)
            {
                _logger.LogDebug("Deleting filter with ID: {FilterId} (Key: {FilterKey})", filter.FilterId, filter.FilterKey);
                var deleteResult = NativeMethods.FwpmFilterDeleteById0(engineHandle, filter.FilterId);

                // FWP_E_FILTER_NOT_FOUND is acceptable (race condition - filter was already removed)
                if (deleteResult == NativeMethods.FWP_E_FILTER_NOT_FOUND)
                {
                    _logger.LogDebug("Filter {FilterId} not found (already removed), continuing", filter.FilterId);
                    continue;
                }

                if (!WfpErrorTranslator.IsSuccess(deleteResult))
                {
                    _logger.LogError("Failed to delete filter {FilterId}: 0x{ErrorCode:X8}", filter.FilterId, deleteResult);
                    // Transaction will be aborted by dispose
                    return WfpErrorTranslator.ToFailedResult<int>(deleteResult, $"Failed to delete filter {filter.FilterId}");
                }

                deletedCount++;
            }

            // Commit transaction
            _logger.LogDebug("Committing WFP transaction");
            var commitResult = transaction.Commit();
            if (commitResult.IsFailure)
            {
                _logger.LogError("Failed to commit transaction: {Error}", commitResult.Error);
                return Result<int>.Failure(commitResult.Error);
            }

            _logger.LogInformation("Successfully removed {DeletedCount} filter(s) from our sublayer", deletedCount);
            return Result<int>.Success(deletedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during filter removal");
            // Transaction will be aborted by dispose
            return Result<int>.Failure(ErrorCodes.WfpError, $"Unexpected error: {ex.Message}");
        }
    }

    // ========================================
    // Policy Application Methods
    // ========================================

    /// <inheritdoc/>
    public Result<ApplyResult> ApplyFilters(List<CompiledFilter> filters)
    {
        _logger.LogInformation("Applying {FilterCount} compiled filter(s) using idempotent reconciliation", filters?.Count ?? 0);

        if (filters == null)
        {
            filters = new List<CompiledFilter>();
        }

        // Open engine session
        var openResult = WfpSession.OpenEngine();
        if (openResult.IsFailure)
        {
            _logger.LogError("Failed to open WFP engine: {Error}", openResult.Error);
            return Result<ApplyResult>.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        var engineHandle = handle.DangerousGetHandle();

        // Step 1: Ensure provider and sublayer exist (outside main transaction)
        var bootstrapResult = EnsureProviderAndSublayerExist();
        if (bootstrapResult.IsFailure)
        {
            _logger.LogError("Bootstrap failed during apply: {Error}", bootstrapResult.Error);
            return Result<ApplyResult>.Failure(bootstrapResult.Error);
        }

        // Step 2: Enumerate existing filters to compute diff
        var enumerateResult = EnumerateFiltersInOurSublayer(engineHandle);
        if (enumerateResult.IsFailure)
        {
            _logger.LogError("Failed to enumerate existing filters: {Error}", enumerateResult.Error);
            return Result<ApplyResult>.Failure(enumerateResult.Error);
        }

        var existingFilters = enumerateResult.Value;
        _logger.LogInformation("Found {ExistingCount} existing filter(s) in our sublayer", existingFilters.Count);

        // Step 3: Compute diff between desired and current state
        var diff = FilterDiffComputer.ComputeDiff(filters, existingFilters);
        _logger.LogInformation("Diff computed: {ToAdd} to add, {ToRemove} to remove, {Unchanged} unchanged",
            diff.ToAdd.Count, diff.ToRemove.Count, diff.Unchanged);

        // If no changes needed, return early (true idempotency)
        if (diff.IsEmpty)
        {
            _logger.LogInformation("No changes needed - policy is already applied (idempotent)");
            return Result<ApplyResult>.Success(new ApplyResult
            {
                FiltersCreated = 0,
                FiltersRemoved = 0,
                FiltersUnchanged = diff.Unchanged
            });
        }

        // Step 4: Begin transaction for atomic apply
        _logger.LogDebug("Beginning WFP transaction for policy reconciliation");
        var txResult = WfpTransaction.Begin(engineHandle);
        if (txResult.IsFailure)
        {
            _logger.LogError("Failed to begin transaction: {Error}", txResult.Error);
            return Result<ApplyResult>.Failure(txResult.Error);
        }

        using var transaction = txResult.Value;

        try
        {
            // Step 5: Remove filters that are no longer in the policy
            int removedCount = 0;
            foreach (var filterKey in diff.ToRemove)
            {
                _logger.LogDebug("Removing obsolete filter: {FilterKey}", filterKey);
                var deleteResult = NativeMethods.FwpmFilterDeleteByKey0(engineHandle, in filterKey);

                if (deleteResult == NativeMethods.FWP_E_FILTER_NOT_FOUND)
                {
                    _logger.LogDebug("Filter {FilterKey} not found (already removed), continuing", filterKey);
                    continue;
                }

                if (!WfpErrorTranslator.IsSuccess(deleteResult))
                {
                    _logger.LogError("Failed to delete filter {FilterKey}: 0x{ErrorCode:X8}", filterKey, deleteResult);
                    return WfpErrorTranslator.ToFailedResult<ApplyResult>(deleteResult, $"Failed to delete filter {filterKey}");
                }

                removedCount++;
            }

            // Step 6: Add new filters that don't exist yet
            int createdCount = 0;
            foreach (var compiledFilter in diff.ToAdd)
            {
                _logger.LogDebug("Creating new filter for rule '{RuleId}': {DisplayName}",
                    compiledFilter.RuleId, compiledFilter.DisplayName);

                var createResult = CreateFilterFromCompiled(engineHandle, compiledFilter);
                if (createResult.IsFailure)
                {
                    _logger.LogError("Failed to create filter for rule '{RuleId}': {Error}",
                        compiledFilter.RuleId, createResult.Error);
                    return Result<ApplyResult>.Failure(createResult.Error);
                }

                createdCount++;
                _logger.LogDebug("Created filter ID {FilterId} for rule '{RuleId}'",
                    createResult.Value, compiledFilter.RuleId);
            }

            // Step 7: Commit transaction
            _logger.LogDebug("Committing WFP transaction");
            var commitResult = transaction.Commit();
            if (commitResult.IsFailure)
            {
                _logger.LogError("Failed to commit transaction: {Error}", commitResult.Error);
                return Result<ApplyResult>.Failure(commitResult.Error);
            }

            _logger.LogInformation("Policy reconciled successfully: {CreatedCount} created, {RemovedCount} removed, {UnchangedCount} unchanged",
                createdCount, removedCount, diff.Unchanged);

            return Result<ApplyResult>.Success(new ApplyResult
            {
                FiltersCreated = createdCount,
                FiltersRemoved = removedCount,
                FiltersUnchanged = diff.Unchanged
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during policy reconciliation");
            return Result<ApplyResult>.Failure(ErrorCodes.WfpError, $"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a WFP filter from a compiled filter definition.
    /// </summary>
    private Result<ulong> CreateFilterFromCompiled(IntPtr engineHandle, CompiledFilter compiled)
    {
        // Count conditions
        int conditionCount = 1; // Protocol is always present
        if (compiled.RemoteIpAddress.HasValue) conditionCount++;
        if (compiled.RemotePort.HasValue || compiled.RemotePortRangeStart.HasValue) conditionCount++;
        if (!string.IsNullOrEmpty(compiled.ProcessPath)) conditionCount++;

        // Allocate memory for conditions
        var conditionSize = Marshal.SizeOf<FWPM_FILTER_CONDITION0>();
        var conditionsPtr = Marshal.AllocHGlobal(conditionSize * conditionCount);
        var allocatedPtrs = new List<IntPtr> { conditionsPtr };

        // Pin the provider GUID
        var providerGuid = WfpConstants.ProviderGuid;
        var providerGuidHandle = GCHandle.Alloc(providerGuid, GCHandleType.Pinned);

        // For app ID blob
        IntPtr appIdBlobPtr = IntPtr.Zero;

        // Allocate memory for weight (FWP_UINT64 requires a pointer to the value)
        var weightPtr = Marshal.AllocHGlobal(sizeof(ulong));
        allocatedPtrs.Add(weightPtr);
        Marshal.WriteInt64(weightPtr, (long)compiled.Weight);

        try
        {
            int conditionIndex = 0;

            // Condition 1: Protocol = TCP (always)
            var protocolCondition = new FWPM_FILTER_CONDITION0
            {
                fieldKey = WfpConditionGuids.FWPM_CONDITION_IP_PROTOCOL,
                matchType = FwpMatchType.FWP_MATCH_EQUAL,
                conditionValue = new FWP_CONDITION_VALUE0
                {
                    type = FwpDataType.FWP_UINT8,
                    value = compiled.Protocol
                }
            };
            Marshal.StructureToPtr(protocolCondition, conditionsPtr + (conditionSize * conditionIndex), false);
            conditionIndex++;

            // Condition 2: Remote IP (if specified)
            if (compiled.RemoteIpAddress.HasValue)
            {
                var addrMaskPtr = Marshal.AllocHGlobal(Marshal.SizeOf<FWP_V4_ADDR_AND_MASK>());
                allocatedPtrs.Add(addrMaskPtr);

                var addrMask = new FWP_V4_ADDR_AND_MASK
                {
                    addr = compiled.RemoteIpAddress.Value,
                    mask = compiled.RemoteIpMask
                };
                Marshal.StructureToPtr(addrMask, addrMaskPtr, false);

                var ipCondition = new FWPM_FILTER_CONDITION0
                {
                    fieldKey = WfpConditionGuids.FWPM_CONDITION_IP_REMOTE_ADDRESS,
                    matchType = FwpMatchType.FWP_MATCH_EQUAL,
                    conditionValue = new FWP_CONDITION_VALUE0
                    {
                        type = FwpDataType.FWP_V4_ADDR_MASK,
                        value = (ulong)addrMaskPtr
                    }
                };
                Marshal.StructureToPtr(ipCondition, conditionsPtr + (conditionSize * conditionIndex), false);
                conditionIndex++;
            }

            // Condition 3: Remote Port (single or range)
            if (compiled.RemotePort.HasValue)
            {
                // Single port match
                var portCondition = new FWPM_FILTER_CONDITION0
                {
                    fieldKey = WfpConditionGuids.FWPM_CONDITION_IP_REMOTE_PORT,
                    matchType = FwpMatchType.FWP_MATCH_EQUAL,
                    conditionValue = new FWP_CONDITION_VALUE0
                    {
                        type = FwpDataType.FWP_UINT16,
                        value = compiled.RemotePort.Value
                    }
                };
                Marshal.StructureToPtr(portCondition, conditionsPtr + (conditionSize * conditionIndex), false);
                conditionIndex++;
            }
            else if (compiled.RemotePortRangeStart.HasValue && compiled.RemotePortRangeEnd.HasValue)
            {
                // Port range match
                var rangePtr = Marshal.AllocHGlobal(Marshal.SizeOf<FWP_RANGE0>());
                allocatedPtrs.Add(rangePtr);

                var range = new FWP_RANGE0
                {
                    valueLow = new FWP_VALUE0
                    {
                        type = FwpDataType.FWP_UINT16,
                        value = compiled.RemotePortRangeStart.Value
                    },
                    valueHigh = new FWP_VALUE0
                    {
                        type = FwpDataType.FWP_UINT16,
                        value = compiled.RemotePortRangeEnd.Value
                    }
                };
                Marshal.StructureToPtr(range, rangePtr, false);

                var portCondition = new FWPM_FILTER_CONDITION0
                {
                    fieldKey = WfpConditionGuids.FWPM_CONDITION_IP_REMOTE_PORT,
                    matchType = FwpMatchType.FWP_MATCH_RANGE,
                    conditionValue = new FWP_CONDITION_VALUE0
                    {
                        type = FwpDataType.FWP_RANGE_TYPE,
                        value = (ulong)rangePtr
                    }
                };
                Marshal.StructureToPtr(portCondition, conditionsPtr + (conditionSize * conditionIndex), false);
                conditionIndex++;
            }

            // Condition 4: Application ID (if specified)
            if (!string.IsNullOrEmpty(compiled.ProcessPath))
            {
                // Get app ID from file name
                var appIdResult = NativeMethods.FwpmGetAppIdFromFileName0(compiled.ProcessPath, out appIdBlobPtr);
                if (!WfpErrorTranslator.IsSuccess(appIdResult))
                {
                    _logger.LogWarning("Failed to get app ID for '{ProcessPath}': 0x{ErrorCode:X8}. Skipping process condition.",
                        compiled.ProcessPath, appIdResult);
                    // Don't fail the whole filter, just skip the process condition
                    conditionCount--;
                }
                else if (appIdBlobPtr != IntPtr.Zero)
                {
                    var appCondition = new FWPM_FILTER_CONDITION0
                    {
                        fieldKey = WfpConditionGuids.FWPM_CONDITION_ALE_APP_ID,
                        matchType = FwpMatchType.FWP_MATCH_EQUAL,
                        conditionValue = new FWP_CONDITION_VALUE0
                        {
                            type = FwpDataType.FWP_BYTE_BLOB_TYPE,
                            value = (ulong)appIdBlobPtr
                        }
                    };
                    Marshal.StructureToPtr(appCondition, conditionsPtr + (conditionSize * conditionIndex), false);
                    conditionIndex++;
                }
            }

            // Determine action type
            var actionType = compiled.Action == FilterAction.Block
                ? FwpActionType.FWP_ACTION_BLOCK
                : FwpActionType.FWP_ACTION_PERMIT;

            // Create the filter structure
            var filter = new FWPM_FILTER0
            {
                filterKey = compiled.FilterKey,
                displayData = new FWPM_DISPLAY_DATA0
                {
                    name = compiled.DisplayName,
                    description = compiled.Description
                },
                flags = FwpmFilterFlags.FWPM_FILTER_FLAG_NONE,
                providerKey = providerGuidHandle.AddrOfPinnedObject(),
                providerData = new FWP_BYTE_BLOB { size = 0, data = IntPtr.Zero },
                layerKey = WfpLayerGuids.FWPM_LAYER_ALE_AUTH_CONNECT_V4,
                subLayerKey = WfpConstants.SublayerGuid,
                weight = new FWP_VALUE0
                {
                    type = FwpDataType.FWP_UINT64,
                    value = (ulong)weightPtr  // FWP_UINT64 requires a pointer to the value
                },
                numFilterConditions = (uint)conditionIndex,
                filterCondition = conditionsPtr,
                action = new FWPM_ACTION0
                {
                    type = actionType,
                    filterType = Guid.Empty
                },
                rawContextOrProviderContextKey = Guid.Empty,
                reserved = IntPtr.Zero,
                filterId = 0,
                effectiveWeight = new FWP_VALUE0 { type = FwpDataType.FWP_EMPTY, value = 0 }
            };

            _logger.LogDebug("Calling FwpmFilterAdd0 for filter {FilterKey}", compiled.FilterKey);
            var result = NativeMethods.FwpmFilterAdd0(engineHandle, in filter, IntPtr.Zero, out ulong filterId);

            if (result == NativeMethods.FWP_E_ALREADY_EXISTS)
            {
                _logger.LogDebug("Filter {FilterKey} already exists, treating as success", compiled.FilterKey);
                return Result<ulong>.Success(0);
            }

            if (!WfpErrorTranslator.IsSuccess(result))
            {
                return WfpErrorTranslator.ToFailedResult<ulong>(result, $"Failed to add filter {compiled.FilterKey}");
            }

            return Result<ulong>.Success(filterId);
        }
        finally
        {
            // Clean up all allocated memory
            foreach (var ptr in allocatedPtrs)
            {
                Marshal.FreeHGlobal(ptr);
            }
            providerGuidHandle.Free();

            // Free app ID blob if allocated by WFP
            if (appIdBlobPtr != IntPtr.Zero)
            {
                NativeMethods.FwpmFreeMemory0(ref appIdBlobPtr);
            }
        }
    }

    /// <summary>
    /// Enumerates all filters in our sublayer, returning their GUIDs and IDs.
    /// </summary>
    /// <param name="engineHandle">Open WFP engine handle.</param>
    /// <returns>List of existing filters in our sublayer.</returns>
    private Result<List<ExistingFilter>> EnumerateFiltersInOurSublayer(IntPtr engineHandle)
    {
        _logger.LogDebug("Enumerating filters in sublayer {SublayerGuid}", WfpConstants.SublayerGuid);

        var filters = new List<ExistingFilter>();
        IntPtr enumHandle = IntPtr.Zero;

        try
        {
            // Note: FWPM_FILTER_ENUM_TEMPLATE0 doesn't have a sublayer field, so we enumerate
            // all filters and filter by sublayerKey client-side. This is the standard approach.

            // Create enumeration handle (enumerate all filters, we'll filter by sublayer client-side)
            var createResult = NativeMethods.FwpmFilterCreateEnumHandle0(
                engineHandle,
                IntPtr.Zero, // null template = enumerate all
                out enumHandle);

            if (!WfpErrorTranslator.IsSuccess(createResult))
            {
                return WfpErrorTranslator.ToFailedResult<List<ExistingFilter>>(createResult, "Failed to create filter enumeration handle");
            }

            _logger.LogDebug("Created filter enumeration handle");

            // Enumerate filters in batches
            const uint batchSize = 100;
            while (true)
            {
                var enumResult = NativeMethods.FwpmFilterEnum0(
                    engineHandle,
                    enumHandle,
                    batchSize,
                    out IntPtr entries,
                    out uint numReturned);

                if (!WfpErrorTranslator.IsSuccess(enumResult))
                {
                    return WfpErrorTranslator.ToFailedResult<List<ExistingFilter>>(enumResult, "Failed to enumerate filters");
                }

                if (numReturned == 0)
                {
                    _logger.LogDebug("Enumeration complete, no more filters");
                    break;
                }

                _logger.LogDebug("Enumerated batch of {Count} filter(s), looking for sublayer {TargetSublayer}",
                    numReturned, WfpConstants.SublayerGuid);

                try
                {
                    // Process the returned filters
                    // entries is a pointer to an array of FWPM_FILTER0* (pointers to filter structures)
                    for (uint i = 0; i < numReturned; i++)
                    {
                        // Read the pointer to the filter structure
                        var filterPtr = Marshal.ReadIntPtr(entries, (int)(i * IntPtr.Size));
                        if (filterPtr == IntPtr.Zero) continue;

                        // Read the FWPM_FILTER0 structure
                        var filter = Marshal.PtrToStructure<FWPM_FILTER0>(filterPtr);

                        // Log all filters for diagnostic purposes (first few only to avoid spam)
                        if (i < 5)
                        {
                            _logger.LogDebug("Filter[{Index}]: Key={FilterKey}, Sublayer={Sublayer}, Name={Name}",
                                i, filter.filterKey, filter.subLayerKey, filter.displayData.name ?? "(null)");
                        }

                        // Check if this filter belongs to our sublayer
                        if (filter.subLayerKey == WfpConstants.SublayerGuid)
                        {
                            _logger.LogDebug("Found filter in our sublayer: ID={FilterId}, Key={FilterKey}, Name={Name}",
                                filter.filterId, filter.filterKey, filter.displayData.name ?? "(unnamed)");
                            filters.Add(new ExistingFilter
                            {
                                FilterKey = filter.filterKey,
                                FilterId = filter.filterId,
                                DisplayName = filter.displayData.name
                            });
                        }
                    }
                }
                finally
                {
                    // Free the memory returned by the enumeration
                    if (entries != IntPtr.Zero)
                    {
                        NativeMethods.FwpmFreeMemory0(ref entries);
                    }
                }
            }

            _logger.LogDebug("Enumeration found {TotalCount} filter(s) in our sublayer", filters.Count);
            return Result<List<ExistingFilter>>.Success(filters);
        }
        finally
        {
            // Clean up the enumeration handle
            if (enumHandle != IntPtr.Zero)
            {
                var destroyResult = NativeMethods.FwpmFilterDestroyEnumHandle0(engineHandle, enumHandle);
                if (!WfpErrorTranslator.IsSuccess(destroyResult))
                {
                    _logger.LogWarning("Failed to destroy enumeration handle: 0x{ErrorCode:X8}", destroyResult);
                }
            }
        }
    }

    private Result<bool> DemoBlockFilterExistsInternal(IntPtr engineHandle)
    {
        var filterGuid = WfpConstants.DemoBlockFilterGuid;
        var result = NativeMethods.FwpmFilterGetByKey0(engineHandle, in filterGuid, out IntPtr filterPtr);

        if (result == NativeMethods.FWP_E_FILTER_NOT_FOUND)
        {
            _logger.LogDebug("Demo block filter does not exist");
            return Result<bool>.Success(false);
        }

        if (!WfpErrorTranslator.IsSuccess(result))
        {
            return WfpErrorTranslator.ToFailedResult<bool>(result, "Failed to check if demo block filter exists");
        }

        // Free the returned memory
        if (filterPtr != IntPtr.Zero)
        {
            NativeMethods.FwpmFreeMemory0(ref filterPtr);
        }

        _logger.LogDebug("Demo block filter exists");
        return Result<bool>.Success(true);
    }

    private Result<ulong> CreateDemoBlockFilterInternal(IntPtr engineHandle)
    {
        _logger.LogDebug("Creating demo block filter structure");

        // Allocate memory for the filter conditions array (3 conditions)
        var conditionSize = Marshal.SizeOf<FWPM_FILTER_CONDITION0>();
        var conditionsPtr = Marshal.AllocHGlobal(conditionSize * 3);

        // Allocate memory for the IPv4 address structure
        var addrMaskPtr = Marshal.AllocHGlobal(Marshal.SizeOf<FWP_V4_ADDR_AND_MASK>());

        // Pin the provider GUID for the filter
        var providerGuid = WfpConstants.ProviderGuid;
        var providerGuidHandle = GCHandle.Alloc(providerGuid, GCHandleType.Pinned);

        try
        {
            // Condition 1: Protocol = TCP (6)
            var protocolCondition = new FWPM_FILTER_CONDITION0
            {
                fieldKey = WfpConditionGuids.FWPM_CONDITION_IP_PROTOCOL,
                matchType = FwpMatchType.FWP_MATCH_EQUAL,
                conditionValue = new FWP_CONDITION_VALUE0
                {
                    type = FwpDataType.FWP_UINT8,
                    value = WfpConstants.ProtocolTcp
                }
            };
            Marshal.StructureToPtr(protocolCondition, conditionsPtr, false);

            // Condition 2: Remote IP = 1.1.1.1
            var addrMask = new FWP_V4_ADDR_AND_MASK
            {
                addr = WfpConstants.DemoBlockRemoteIp,
                mask = 0xFFFFFFFF // Exact match
            };
            Marshal.StructureToPtr(addrMask, addrMaskPtr, false);

            var ipCondition = new FWPM_FILTER_CONDITION0
            {
                fieldKey = WfpConditionGuids.FWPM_CONDITION_IP_REMOTE_ADDRESS,
                matchType = FwpMatchType.FWP_MATCH_EQUAL,
                conditionValue = new FWP_CONDITION_VALUE0
                {
                    type = FwpDataType.FWP_V4_ADDR_MASK,
                    value = (ulong)addrMaskPtr
                }
            };
            Marshal.StructureToPtr(ipCondition, conditionsPtr + conditionSize, false);

            // Condition 3: Remote Port = 443
            var portCondition = new FWPM_FILTER_CONDITION0
            {
                fieldKey = WfpConditionGuids.FWPM_CONDITION_IP_REMOTE_PORT,
                matchType = FwpMatchType.FWP_MATCH_EQUAL,
                conditionValue = new FWP_CONDITION_VALUE0
                {
                    type = FwpDataType.FWP_UINT16,
                    value = WfpConstants.DemoBlockRemotePort
                }
            };
            Marshal.StructureToPtr(portCondition, conditionsPtr + (conditionSize * 2), false);

            // Create the filter structure
            var filter = new FWPM_FILTER0
            {
                filterKey = WfpConstants.DemoBlockFilterGuid,
                displayData = new FWPM_DISPLAY_DATA0
                {
                    name = WfpConstants.DemoBlockFilterName,
                    description = WfpConstants.DemoBlockFilterDescription
                },
                flags = FwpmFilterFlags.FWPM_FILTER_FLAG_NONE,
                providerKey = providerGuidHandle.AddrOfPinnedObject(),
                providerData = new FWP_BYTE_BLOB { size = 0, data = IntPtr.Zero },
                layerKey = WfpLayerGuids.FWPM_LAYER_ALE_AUTH_CONNECT_V4,
                subLayerKey = WfpConstants.SublayerGuid,
                weight = new FWP_VALUE0
                {
                    type = FwpDataType.FWP_EMPTY, // Let WFP auto-calculate based on conditions
                    value = 0
                },
                numFilterConditions = 3,
                filterCondition = conditionsPtr,
                action = new FWPM_ACTION0
                {
                    type = FwpActionType.FWP_ACTION_BLOCK,
                    filterType = Guid.Empty
                },
                rawContextOrProviderContextKey = Guid.Empty,
                reserved = IntPtr.Zero,
                filterId = 0,
                effectiveWeight = new FWP_VALUE0 { type = FwpDataType.FWP_EMPTY, value = 0 }
            };

            _logger.LogDebug("Calling FwpmFilterAdd0 to add demo block filter");
            var result = NativeMethods.FwpmFilterAdd0(engineHandle, in filter, IntPtr.Zero, out ulong filterId);

            // Handle "already exists" as success for idempotency
            if (result == NativeMethods.FWP_E_ALREADY_EXISTS)
            {
                _logger.LogDebug("Demo block filter already exists (race condition), treating as success");
                return Result<ulong>.Success(0);
            }

            if (!WfpErrorTranslator.IsSuccess(result))
            {
                return WfpErrorTranslator.ToFailedResult<ulong>(result, "Failed to add demo block filter");
            }

            _logger.LogDebug("FwpmFilterAdd0 returned filter ID: {FilterId}", filterId);
            return Result<ulong>.Success(filterId);
        }
        finally
        {
            // Clean up all allocated memory
            Marshal.FreeHGlobal(conditionsPtr);
            Marshal.FreeHGlobal(addrMaskPtr);
            providerGuidHandle.Free();
        }
    }
}
