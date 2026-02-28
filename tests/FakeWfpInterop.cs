// tests/FakeWfpInterop.cs
// Fake implementation of IWfpInterop for unit testing WfpEngine
// Phase 19: WFP Mocking Refactor

using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Native;
using WfpTrafficControl.Shared.Policy;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Fake implementation of <see cref="IWfpInterop"/> for unit testing.
/// Simulates WFP behavior in memory without actual P/Invoke calls.
/// </summary>
public sealed class FakeWfpInterop : IWfpInterop
{
    // ========================================
    // State
    // ========================================

    public bool ProviderExistsValue { get; set; }
    public bool SublayerExistsValue { get; set; }

    /// <summary>
    /// Simulated filters in the sublayer. Key is FilterKey GUID, Value is (FilterId, DisplayName).
    /// </summary>
    private readonly Dictionary<Guid, (ulong FilterId, string? DisplayName)> _filters = new();

    /// <summary>
    /// Counter for generating unique filter IDs.
    /// </summary>
    private ulong _nextFilterId = 1;

    // ========================================
    // Failure Injection
    // ========================================

    /// <summary>
    /// If set, OpenEngine will return this error.
    /// </summary>
    public Error? OpenEngineError { get; set; }

    /// <summary>
    /// If set, AddProvider will return this error.
    /// </summary>
    public Error? AddProviderError { get; set; }

    /// <summary>
    /// If set, AddSublayer will return this error.
    /// </summary>
    public Error? AddSublayerError { get; set; }

    /// <summary>
    /// If set, DeleteSublayer will return this error.
    /// </summary>
    public Error? DeleteSublayerError { get; set; }

    /// <summary>
    /// If set, EnumerateFiltersInSublayer will return this error.
    /// </summary>
    public Error? EnumerateFiltersError { get; set; }

    /// <summary>
    /// If set, AddFilter will return this error.
    /// </summary>
    public Error? AddFilterError { get; set; }

    /// <summary>
    /// If set, DeleteFilterByKey will return this error.
    /// </summary>
    public Error? DeleteFilterByKeyError { get; set; }

    /// <summary>
    /// If set, DeleteFilterById will return this error.
    /// </summary>
    public Error? DeleteFilterByIdError { get; set; }

    // ========================================
    // Call Tracking
    // ========================================

    public int OpenEngineCallCount { get; private set; }
    public int AddProviderCallCount { get; private set; }
    public int AddSublayerCallCount { get; private set; }
    public int DeleteProviderCallCount { get; private set; }
    public int DeleteSublayerCallCount { get; private set; }
    public int EnumerateFiltersCallCount { get; private set; }
    public int AddFilterCallCount { get; private set; }
    public int DeleteFilterByKeyCallCount { get; private set; }
    public int DeleteFilterByIdCallCount { get; private set; }

    /// <summary>
    /// List of filter keys that were added (in order).
    /// </summary>
    public List<Guid> AddedFilterKeys { get; } = new();

    /// <summary>
    /// List of filter keys that were deleted (in order).
    /// </summary>
    public List<Guid> DeletedFilterKeys { get; } = new();

    /// <summary>
    /// List of filter IDs that were deleted (in order).
    /// </summary>
    public List<ulong> DeletedFilterIds { get; } = new();

    // ========================================
    // Helper Methods for Test Setup
    // ========================================

    /// <summary>
    /// Adds a filter to the fake store.
    /// </summary>
    public void AddExistingFilter(Guid filterKey, ulong? filterId = null, string? displayName = null)
    {
        var id = filterId ?? _nextFilterId++;
        _filters[filterKey] = (id, displayName);
    }

    /// <summary>
    /// Gets the current filter count.
    /// </summary>
    public int FilterCount => _filters.Count;

    /// <summary>
    /// Resets all call counts and tracking lists.
    /// </summary>
    public void ResetCallTracking()
    {
        OpenEngineCallCount = 0;
        AddProviderCallCount = 0;
        AddSublayerCallCount = 0;
        DeleteProviderCallCount = 0;
        DeleteSublayerCallCount = 0;
        EnumerateFiltersCallCount = 0;
        AddFilterCallCount = 0;
        DeleteFilterByKeyCallCount = 0;
        DeleteFilterByIdCallCount = 0;
        AddedFilterKeys.Clear();
        DeletedFilterKeys.Clear();
        DeletedFilterIds.Clear();
    }

    /// <summary>
    /// Clears all filters from the fake store.
    /// </summary>
    public void ClearFilters()
    {
        _filters.Clear();
    }

    // ========================================
    // IWfpInterop Implementation
    // ========================================

    public Result<WfpEngineHandle> OpenEngine()
    {
        OpenEngineCallCount++;

        if (OpenEngineError != null)
        {
            return Result<WfpEngineHandle>.Failure(OpenEngineError);
        }

        // Create a fake handle using reflection since internal constructor
        // isn't accessible across TFM boundaries (net8.0 vs net8.0-windows7.0)
        return Result<WfpEngineHandle>.Success(CreateFakeHandle());
    }

    /// <summary>
    /// Creates a fake WfpEngineHandle using reflection.
    /// </summary>
    private static WfpEngineHandle CreateFakeHandle()
    {
        var ctor = typeof(WfpEngineHandle).GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(IntPtr), typeof(bool) },
            null);

        if (ctor == null)
        {
            throw new InvalidOperationException("Could not find WfpEngineHandle constructor");
        }

        // Use a non-zero value since WfpTransaction.Begin checks for zero handle
        // ownsHandle=false so we don't try to close this fake handle
        var fakePtr = new IntPtr(0x12345678);
        return (WfpEngineHandle)ctor.Invoke(new object[] { fakePtr, false });
    }

    public Result<bool> ProviderExists(IntPtr engineHandle)
    {
        return Result<bool>.Success(ProviderExistsValue);
    }

    public Result AddProvider(IntPtr engineHandle)
    {
        AddProviderCallCount++;

        if (AddProviderError != null)
        {
            return Result.Failure(AddProviderError);
        }

        ProviderExistsValue = true;
        return Result.Success();
    }

    public Result DeleteProvider(IntPtr engineHandle)
    {
        DeleteProviderCallCount++;
        ProviderExistsValue = false;
        return Result.Success();
    }

    public Result<bool> SublayerExists(IntPtr engineHandle)
    {
        return Result<bool>.Success(SublayerExistsValue);
    }

    public Result AddSublayer(IntPtr engineHandle)
    {
        AddSublayerCallCount++;

        if (AddSublayerError != null)
        {
            return Result.Failure(AddSublayerError);
        }

        SublayerExistsValue = true;
        return Result.Success();
    }

    public Result DeleteSublayer(IntPtr engineHandle)
    {
        DeleteSublayerCallCount++;

        if (DeleteSublayerError != null)
        {
            return Result.Failure(DeleteSublayerError);
        }

        // Simulate FWP_E_IN_USE if filters exist
        if (_filters.Count > 0)
        {
            return Result.Failure(ErrorCodes.WfpError, "Cannot remove sublayer: filters still exist.");
        }

        SublayerExistsValue = false;
        return Result.Success();
    }

    public Result<List<ExistingFilter>> EnumerateFiltersInSublayer(IntPtr engineHandle)
    {
        EnumerateFiltersCallCount++;

        if (EnumerateFiltersError != null)
        {
            return Result<List<ExistingFilter>>.Failure(EnumerateFiltersError);
        }

        var filters = _filters.Select(kv => new ExistingFilter
        {
            FilterKey = kv.Key,
            FilterId = kv.Value.FilterId,
            DisplayName = kv.Value.DisplayName
        }).ToList();

        return Result<List<ExistingFilter>>.Success(filters);
    }

    public Result<ulong> AddFilter(IntPtr engineHandle, CompiledFilter filter)
    {
        AddFilterCallCount++;
        AddedFilterKeys.Add(filter.FilterKey);

        if (AddFilterError != null)
        {
            return Result<ulong>.Failure(AddFilterError);
        }

        // Simulate idempotent behavior - if already exists, return success
        if (_filters.TryGetValue(filter.FilterKey, out var existing))
        {
            return Result<ulong>.Success(existing.FilterId);
        }

        var filterId = _nextFilterId++;
        _filters[filter.FilterKey] = (filterId, filter.DisplayName);
        return Result<ulong>.Success(filterId);
    }

    public Result DeleteFilterByKey(IntPtr engineHandle, Guid filterKey)
    {
        DeleteFilterByKeyCallCount++;
        DeletedFilterKeys.Add(filterKey);

        if (DeleteFilterByKeyError != null)
        {
            return Result.Failure(DeleteFilterByKeyError);
        }

        // Idempotent - not found is success
        _filters.Remove(filterKey);
        return Result.Success();
    }

    public Result DeleteFilterById(IntPtr engineHandle, ulong filterId)
    {
        DeleteFilterByIdCallCount++;
        DeletedFilterIds.Add(filterId);

        if (DeleteFilterByIdError != null)
        {
            return Result.Failure(DeleteFilterByIdError);
        }

        // Find and remove the filter with this ID
        var key = _filters.FirstOrDefault(kv => kv.Value.FilterId == filterId).Key;
        if (key != Guid.Empty)
        {
            _filters.Remove(key);
        }

        // Idempotent - not found is success
        return Result.Success();
    }

    public Result<bool> FilterExists(IntPtr engineHandle, Guid filterKey)
    {
        return Result<bool>.Success(_filters.ContainsKey(filterKey));
    }
}

/// <summary>
/// Fake implementation of <see cref="IWfpNativeTransaction"/> for unit testing.
/// Always returns success for all transaction operations.
/// </summary>
public sealed class FakeWfpNativeTransaction : IWfpNativeTransaction
{
    // Call tracking
    public int BeginCallCount { get; private set; }
    public int CommitCallCount { get; private set; }
    public int AbortCallCount { get; private set; }

    // Error injection
    public uint? BeginError { get; set; }
    public uint? CommitError { get; set; }
    public uint? AbortError { get; set; }

    public uint Begin(IntPtr engineHandle)
    {
        BeginCallCount++;
        return BeginError ?? 0; // ERROR_SUCCESS
    }

    public uint Commit(IntPtr engineHandle)
    {
        CommitCallCount++;
        return CommitError ?? 0; // ERROR_SUCCESS
    }

    public uint Abort(IntPtr engineHandle)
    {
        AbortCallCount++;
        return AbortError ?? 0; // ERROR_SUCCESS
    }

    public void Reset()
    {
        BeginCallCount = 0;
        CommitCallCount = 0;
        AbortCallCount = 0;
        BeginError = null;
        CommitError = null;
        AbortError = null;
    }
}
