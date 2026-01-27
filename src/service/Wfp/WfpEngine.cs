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
}
