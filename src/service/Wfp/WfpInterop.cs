// src/service/Wfp/WfpInterop.cs
// Production implementation of IWfpInterop using native P/Invoke
// Phase 19: WFP Mocking Refactor

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Native;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Service.Wfp;

/// <summary>
/// Production implementation of <see cref="IWfpInterop"/> using native P/Invoke calls.
/// This class encapsulates all direct WFP API interactions.
/// </summary>
public sealed class WfpInterop : IWfpInterop
{
    private readonly ILogger<WfpInterop> _logger;

    /// <summary>
    /// Sublayer weight. Higher values have higher priority.
    /// We use a moderate weight to avoid conflicts with system sublayers.
    /// </summary>
    private const ushort SublayerWeight = 0x8000; // 32768 - middle of the range

    public WfpInterop(ILogger<WfpInterop> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ========================================
    // Engine Session Management
    // ========================================

    /// <inheritdoc/>
    public Result<WfpEngineHandle> OpenEngine()
    {
        return WfpSession.OpenEngine();
    }

    // ========================================
    // Provider Operations
    // ========================================

    /// <inheritdoc/>
    public Result<bool> ProviderExists(IntPtr engineHandle)
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

    /// <inheritdoc/>
    public Result AddProvider(IntPtr engineHandle)
    {
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

    /// <inheritdoc/>
    public Result DeleteProvider(IntPtr engineHandle)
    {
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

    // ========================================
    // Sublayer Operations
    // ========================================

    /// <inheritdoc/>
    public Result<bool> SublayerExists(IntPtr engineHandle)
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

    /// <inheritdoc/>
    public Result AddSublayer(IntPtr engineHandle)
    {
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

    /// <inheritdoc/>
    public Result DeleteSublayer(IntPtr engineHandle)
    {
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
    // Filter Operations
    // ========================================

    /// <inheritdoc/>
    public Result<List<ExistingFilter>> EnumerateFiltersInSublayer(IntPtr engineHandle)
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

    /// <inheritdoc/>
    public Result<ulong> AddFilter(IntPtr engineHandle, CompiledFilter filter)
    {
        // Count conditions
        int conditionCount = 1; // Protocol is always present
        if (filter.RemoteIpAddress.HasValue) conditionCount++;
        if (filter.RemotePort.HasValue || filter.RemotePortRangeStart.HasValue) conditionCount++;
        if (!string.IsNullOrEmpty(filter.ProcessPath)) conditionCount++;

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
        Marshal.WriteInt64(weightPtr, (long)filter.Weight);

        try
        {
            int conditionIndex = 0;

            // Condition 1: Protocol (always)
            var protocolCondition = new FWPM_FILTER_CONDITION0
            {
                fieldKey = WfpConditionGuids.FWPM_CONDITION_IP_PROTOCOL,
                matchType = FwpMatchType.FWP_MATCH_EQUAL,
                conditionValue = new FWP_CONDITION_VALUE0
                {
                    type = FwpDataType.FWP_UINT8,
                    value = filter.Protocol
                }
            };
            Marshal.StructureToPtr(protocolCondition, conditionsPtr + (conditionSize * conditionIndex), false);
            conditionIndex++;

            // Condition 2: Remote IP (if specified)
            if (filter.RemoteIpAddress.HasValue)
            {
                var addrMaskPtr = Marshal.AllocHGlobal(Marshal.SizeOf<FWP_V4_ADDR_AND_MASK>());
                allocatedPtrs.Add(addrMaskPtr);

                var addrMask = new FWP_V4_ADDR_AND_MASK
                {
                    addr = filter.RemoteIpAddress.Value,
                    mask = filter.RemoteIpMask
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
            if (filter.RemotePort.HasValue)
            {
                // Single port match
                var portCondition = new FWPM_FILTER_CONDITION0
                {
                    fieldKey = WfpConditionGuids.FWPM_CONDITION_IP_REMOTE_PORT,
                    matchType = FwpMatchType.FWP_MATCH_EQUAL,
                    conditionValue = new FWP_CONDITION_VALUE0
                    {
                        type = FwpDataType.FWP_UINT16,
                        value = filter.RemotePort.Value
                    }
                };
                Marshal.StructureToPtr(portCondition, conditionsPtr + (conditionSize * conditionIndex), false);
                conditionIndex++;
            }
            else if (filter.RemotePortRangeStart.HasValue && filter.RemotePortRangeEnd.HasValue)
            {
                // Port range match
                var rangePtr = Marshal.AllocHGlobal(Marshal.SizeOf<FWP_RANGE0>());
                allocatedPtrs.Add(rangePtr);

                var range = new FWP_RANGE0
                {
                    valueLow = new FWP_VALUE0
                    {
                        type = FwpDataType.FWP_UINT16,
                        value = filter.RemotePortRangeStart.Value
                    },
                    valueHigh = new FWP_VALUE0
                    {
                        type = FwpDataType.FWP_UINT16,
                        value = filter.RemotePortRangeEnd.Value
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
            if (!string.IsNullOrEmpty(filter.ProcessPath))
            {
                // Get app ID from file name
                var appIdResult = NativeMethods.FwpmGetAppIdFromFileName0(filter.ProcessPath, out appIdBlobPtr);
                if (!WfpErrorTranslator.IsSuccess(appIdResult))
                {
                    // SECURITY: Do NOT silently drop the process condition!
                    // If we can't resolve the app ID (e.g., executable doesn't exist), we must fail
                    // the entire filter creation. Otherwise, a typo'd process path would create a
                    // filter matching ALL processes, which is a privilege escalation vector.
                    _logger.LogError("Failed to resolve app ID for '{ProcessPath}': 0x{ErrorCode:X8}. " +
                        "Filter creation aborted to prevent overly-broad rule.",
                        filter.ProcessPath, appIdResult);
                    return Result<ulong>.Failure(new Error(
                        ErrorCodes.InvalidArgument,
                        $"Cannot create filter: process path '{filter.ProcessPath}' could not be resolved. " +
                        "Ensure the executable exists and the path is correct."));
                }

                if (appIdBlobPtr != IntPtr.Zero)
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
            var actionType = filter.Action == FilterAction.Block
                ? FwpActionType.FWP_ACTION_BLOCK
                : FwpActionType.FWP_ACTION_PERMIT;

            // Select the appropriate WFP layer based on direction
            var layerKey = string.Equals(filter.Direction, RuleDirection.Inbound, StringComparison.OrdinalIgnoreCase)
                ? WfpLayerGuids.FWPM_LAYER_ALE_AUTH_RECV_ACCEPT_V4
                : WfpLayerGuids.FWPM_LAYER_ALE_AUTH_CONNECT_V4;

            // Create the filter structure
            var wfpFilter = new FWPM_FILTER0
            {
                filterKey = filter.FilterKey,
                displayData = new FWPM_DISPLAY_DATA0
                {
                    name = filter.DisplayName,
                    description = filter.Description
                },
                flags = FwpmFilterFlags.FWPM_FILTER_FLAG_NONE,
                providerKey = providerGuidHandle.AddrOfPinnedObject(),
                providerData = new FWP_BYTE_BLOB { size = 0, data = IntPtr.Zero },
                layerKey = layerKey,
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

            _logger.LogDebug("Calling FwpmFilterAdd0 for filter {FilterKey}", filter.FilterKey);
            var result = NativeMethods.FwpmFilterAdd0(engineHandle, in wfpFilter, IntPtr.Zero, out ulong filterId);

            if (result == NativeMethods.FWP_E_ALREADY_EXISTS)
            {
                _logger.LogDebug("Filter {FilterKey} already exists, treating as success", filter.FilterKey);
                return Result<ulong>.Success(0);
            }

            if (!WfpErrorTranslator.IsSuccess(result))
            {
                return WfpErrorTranslator.ToFailedResult<ulong>(result, $"Failed to add filter {filter.FilterKey}");
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

    /// <inheritdoc/>
    public Result DeleteFilterByKey(IntPtr engineHandle, Guid filterKey)
    {
        _logger.LogDebug("Deleting filter by key: {FilterKey}", filterKey);
        var result = NativeMethods.FwpmFilterDeleteByKey0(engineHandle, in filterKey);

        // Not found is success for idempotent delete
        if (result == NativeMethods.FWP_E_FILTER_NOT_FOUND)
        {
            _logger.LogDebug("Filter {FilterKey} not found, treating as success", filterKey);
            return Result.Success();
        }

        if (!WfpErrorTranslator.IsSuccess(result))
        {
            return WfpErrorTranslator.ToFailedResult(result, $"Failed to delete filter {filterKey}");
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public Result DeleteFilterById(IntPtr engineHandle, ulong filterId)
    {
        _logger.LogDebug("Deleting filter by ID: {FilterId}", filterId);
        var result = NativeMethods.FwpmFilterDeleteById0(engineHandle, filterId);

        // Not found is success for idempotent delete
        if (result == NativeMethods.FWP_E_FILTER_NOT_FOUND)
        {
            _logger.LogDebug("Filter ID {FilterId} not found, treating as success", filterId);
            return Result.Success();
        }

        if (!WfpErrorTranslator.IsSuccess(result))
        {
            return WfpErrorTranslator.ToFailedResult(result, $"Failed to delete filter ID {filterId}");
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public Result<bool> FilterExists(IntPtr engineHandle, Guid filterKey)
    {
        var result = NativeMethods.FwpmFilterGetByKey0(engineHandle, in filterKey, out IntPtr filterPtr);

        if (result == NativeMethods.FWP_E_FILTER_NOT_FOUND)
        {
            _logger.LogDebug("Filter {FilterKey} does not exist", filterKey);
            return Result<bool>.Success(false);
        }

        if (!WfpErrorTranslator.IsSuccess(result))
        {
            return WfpErrorTranslator.ToFailedResult<bool>(result, $"Failed to check if filter {filterKey} exists");
        }

        // Free the returned memory
        if (filterPtr != IntPtr.Zero)
        {
            NativeMethods.FwpmFreeMemory0(ref filterPtr);
        }

        _logger.LogDebug("Filter {FilterKey} exists", filterKey);
        return Result<bool>.Success(true);
    }
}
