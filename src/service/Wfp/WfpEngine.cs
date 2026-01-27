using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Native;

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
    public Result RemoveAllFilters()
    {
        _logger.LogInformation("Removing all filters from our sublayer");

        // For now, we only have the demo block filter
        // In the future, this would enumerate and remove all filters in our sublayer
        return RemoveDemoBlockFilter();
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
                rawContext = 0,
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
