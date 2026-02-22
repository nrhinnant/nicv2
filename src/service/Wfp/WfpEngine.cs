using Microsoft.Extensions.Logging;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Native;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Service.Wfp;

/// <summary>
/// Implementation of WFP engine operations using the Windows Filtering Platform APIs.
/// All operations use GUIDs and display names from <see cref="WfpConstants"/>.
/// </summary>
/// <remarks>
/// This class uses <see cref="IWfpInterop"/> for all WFP operations, enabling
/// unit testing with a fake implementation. For production, use <see cref="WfpInterop"/>.
/// </remarks>
public sealed class WfpEngine : IWfpEngine
{
    private readonly ILogger<WfpEngine> _logger;
    private readonly IWfpInterop _interop;
    private readonly IWfpNativeTransaction? _transactionOverride;

    /// <summary>
    /// Creates a WfpEngine for production use.
    /// </summary>
    public WfpEngine(ILogger<WfpEngine> logger, IWfpInterop interop)
        : this(logger, interop, null)
    {
    }

    /// <summary>
    /// Creates a WfpEngine with optional transaction override for testing.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="interop">WFP interop implementation.</param>
    /// <param name="transactionOverride">Optional transaction implementation for testing. Pass null for production.</param>
    public WfpEngine(ILogger<WfpEngine> logger, IWfpInterop interop, IWfpNativeTransaction? transactionOverride)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interop = interop ?? throw new ArgumentNullException(nameof(interop));
        _transactionOverride = transactionOverride;
    }

    /// <inheritdoc/>
    public Result EnsureProviderAndSublayerExist()
    {
        _logger.LogInformation("Ensuring WFP provider and sublayer exist");

        // Open engine session
        var openResult = _interop.OpenEngine();
        if (openResult.IsFailure)
        {
            _logger.LogError("Failed to open WFP engine: {Error}", openResult.Error);
            return Result.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        var engineHandle = handle.DangerousGetHandle();

        // Begin transaction for atomicity - auto-aborts on dispose if not committed
        _logger.LogDebug("Beginning WFP transaction");
        var txResult = WfpTransaction.Begin(engineHandle, _transactionOverride);
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
        var openResult = _interop.OpenEngine();
        if (openResult.IsFailure)
        {
            _logger.LogError("Failed to open WFP engine: {Error}", openResult.Error);
            return Result.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        var engineHandle = handle.DangerousGetHandle();

        // Begin transaction for atomicity - auto-aborts on dispose if not committed
        _logger.LogDebug("Beginning WFP transaction");
        var txResult = WfpTransaction.Begin(engineHandle, _transactionOverride);
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
        var openResult = _interop.OpenEngine();
        if (openResult.IsFailure)
        {
            return Result<bool>.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        return _interop.ProviderExists(handle.DangerousGetHandle());
    }

    /// <inheritdoc/>
    public Result<bool> SublayerExists()
    {
        var openResult = _interop.OpenEngine();
        if (openResult.IsFailure)
        {
            return Result<bool>.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        return _interop.SublayerExists(handle.DangerousGetHandle());
    }

    // ========================================
    // Internal Helper Methods
    // ========================================

    private Result EnsureProviderExistsInternal(IntPtr engineHandle)
    {
        // Check if already exists
        var existsResult = _interop.ProviderExists(engineHandle);
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
        return _interop.AddProvider(engineHandle);
    }

    private Result EnsureSublayerExistsInternal(IntPtr engineHandle)
    {
        // Check if already exists
        var existsResult = _interop.SublayerExists(engineHandle);
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
        return _interop.AddSublayer(engineHandle);
    }

    private Result RemoveProviderInternal(IntPtr engineHandle)
    {
        // Check if exists
        var existsResult = _interop.ProviderExists(engineHandle);
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
        return _interop.DeleteProvider(engineHandle);
    }

    private Result RemoveSublayerInternal(IntPtr engineHandle)
    {
        // Check if exists
        var existsResult = _interop.SublayerExists(engineHandle);
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
        return _interop.DeleteSublayer(engineHandle);
    }

    // ========================================
    // Demo Block Filter Methods
    // ========================================

    /// <inheritdoc/>
    public Result AddDemoBlockFilter()
    {
        _logger.LogInformation("Adding demo block filter (block TCP to 1.1.1.1:443)");

        // Open engine session
        var openResult = _interop.OpenEngine();
        if (openResult.IsFailure)
        {
            _logger.LogError("Failed to open WFP engine: {Error}", openResult.Error);
            return Result.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        var engineHandle = handle.DangerousGetHandle();

        // Begin transaction
        _logger.LogDebug("Beginning WFP transaction for demo filter");
        var txResult = WfpTransaction.Begin(engineHandle, _transactionOverride);
        if (txResult.IsFailure)
        {
            _logger.LogError("Failed to begin transaction: {Error}", txResult.Error);
            return Result.Failure(txResult.Error);
        }

        using var transaction = txResult.Value;

        try
        {
            // Check if already exists (idempotent)
            var existsResult = _interop.FilterExists(engineHandle, WfpConstants.DemoBlockFilterGuid);
            if (existsResult.IsFailure)
            {
                return Result.Failure(existsResult.Error);
            }

            if (existsResult.Value)
            {
                _logger.LogDebug("Demo block filter already exists, skipping creation");
                // Still commit the empty transaction for consistency
                var earlyCommitResult = transaction.Commit();
                if (earlyCommitResult.IsFailure)
                {
                    _logger.LogError("Failed to commit transaction: {Error}", earlyCommitResult.Error);
                    return earlyCommitResult;
                }
                return Result.Success();
            }

            // Create the demo filter as a CompiledFilter
            var demoFilter = CreateDemoBlockCompiledFilter();

            // Add the filter
            var createResult = _interop.AddFilter(engineHandle, demoFilter);
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
        var openResult = _interop.OpenEngine();
        if (openResult.IsFailure)
        {
            _logger.LogError("Failed to open WFP engine: {Error}", openResult.Error);
            return Result.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        var engineHandle = handle.DangerousGetHandle();

        // Begin transaction
        _logger.LogDebug("Beginning WFP transaction for removing demo filter");
        var txResult = WfpTransaction.Begin(engineHandle, _transactionOverride);
        if (txResult.IsFailure)
        {
            _logger.LogError("Failed to begin transaction: {Error}", txResult.Error);
            return Result.Failure(txResult.Error);
        }

        using var transaction = txResult.Value;

        try
        {
            // Delete by GUID key
            var deleteResult = _interop.DeleteFilterByKey(engineHandle, WfpConstants.DemoBlockFilterGuid);
            if (deleteResult.IsFailure)
            {
                _logger.LogError("Failed to delete demo block filter: {Error}", deleteResult.Error);
                return deleteResult;
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
        var openResult = _interop.OpenEngine();
        if (openResult.IsFailure)
        {
            return Result<bool>.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        return _interop.FilterExists(handle.DangerousGetHandle(), WfpConstants.DemoBlockFilterGuid);
    }

    /// <inheritdoc/>
    public Result<int> RemoveAllFilters()
    {
        _logger.LogInformation("Removing all filters from our sublayer (panic rollback)");

        // Open engine session
        var openResult = _interop.OpenEngine();
        if (openResult.IsFailure)
        {
            _logger.LogError("Failed to open WFP engine: {Error}", openResult.Error);
            return Result<int>.Failure(openResult.Error);
        }

        using var handle = openResult.Value;
        var engineHandle = handle.DangerousGetHandle();

        // Phase 1: Enumerate filters in our sublayer (outside transaction)
        var enumerateResult = _interop.EnumerateFiltersInSublayer(engineHandle);
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
        var txResult = WfpTransaction.Begin(engineHandle, _transactionOverride);
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
                var deleteResult = _interop.DeleteFilterById(engineHandle, filter.FilterId);

                if (deleteResult.IsFailure)
                {
                    _logger.LogError("Failed to delete filter {FilterId}: {Error}", filter.FilterId, deleteResult.Error);
                    // Transaction will be aborted by dispose
                    return Result<int>.Failure(deleteResult.Error);
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
        var openResult = _interop.OpenEngine();
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
        var enumerateResult = _interop.EnumerateFiltersInSublayer(engineHandle);
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
        var txResult = WfpTransaction.Begin(engineHandle, _transactionOverride);
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
                var deleteResult = _interop.DeleteFilterByKey(engineHandle, filterKey);

                if (deleteResult.IsFailure)
                {
                    _logger.LogError("Failed to delete filter {FilterKey}: {Error}", filterKey, deleteResult.Error);
                    return Result<ApplyResult>.Failure(deleteResult.Error);
                }

                removedCount++;
            }

            // Step 6: Add new filters that don't exist yet
            int createdCount = 0;
            foreach (var compiledFilter in diff.ToAdd)
            {
                _logger.LogDebug("Creating new filter for rule '{RuleId}': {DisplayName}",
                    compiledFilter.RuleId, compiledFilter.DisplayName);

                var createResult = _interop.AddFilter(engineHandle, compiledFilter);
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

    // ========================================
    // Helper Methods
    // ========================================

    /// <summary>
    /// Creates a CompiledFilter representing the demo block filter.
    /// </summary>
    private static CompiledFilter CreateDemoBlockCompiledFilter()
    {
        return new CompiledFilter
        {
            FilterKey = WfpConstants.DemoBlockFilterGuid,
            DisplayName = WfpConstants.DemoBlockFilterName,
            Description = WfpConstants.DemoBlockFilterDescription,
            Action = FilterAction.Block,
            Weight = 0, // Let WFP auto-calculate based on conditions
            RuleId = "demo-block",
            Protocol = WfpConstants.ProtocolTcp,
            Direction = RuleDirection.Outbound,
            RemoteIpAddress = WfpConstants.DemoBlockRemoteIp,
            RemoteIpMask = 0xFFFFFFFF, // Exact match
            RemotePort = WfpConstants.DemoBlockRemotePort,
            RemotePortRangeStart = null,
            RemotePortRangeEnd = null,
            ProcessPath = null
        };
    }
}
