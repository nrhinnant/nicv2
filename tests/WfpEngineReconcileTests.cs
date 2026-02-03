// tests/WfpEngineReconcileTests.cs
// Unit tests for WfpEngine reconciliation logic using FakeWfpInterop
// Phase 19: WFP Mocking Refactor

using Microsoft.Extensions.Logging.Abstractions;
using WfpTrafficControl.Service.Wfp;
using WfpTrafficControl.Shared;
using WfpTrafficControl.Shared.Native;
using WfpTrafficControl.Shared.Policy;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Unit tests for WfpEngine.ApplyFilters() reconciliation logic.
/// Uses FakeWfpInterop to test without actual WFP access.
/// </summary>
public class WfpEngineReconcileTests
{
    private readonly FakeWfpInterop _fake;
    private readonly FakeWfpNativeTransaction _fakeTransaction;
    private readonly WfpEngine _engine;

    public WfpEngineReconcileTests()
    {
        _fake = new FakeWfpInterop();
        _fakeTransaction = new FakeWfpNativeTransaction();
        _engine = new WfpEngine(NullLogger<WfpEngine>.Instance, _fake, _fakeTransaction);
    }

    // ========================================
    // Helper Methods
    // ========================================

    private static CompiledFilter CreateFilter(string ruleId, int portIndex = 0)
    {
        // Generate a deterministic GUID (same as RuleCompiler.GenerateFilterGuid)
        var input = $"{ruleId}:{portIndex}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x40);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        var filterKey = new Guid(hash);

        return new CompiledFilter
        {
            FilterKey = filterKey,
            DisplayName = $"Test Filter: {ruleId}",
            Description = $"Test filter for rule {ruleId}",
            Action = FilterAction.Block,
            Weight = 1000,
            RuleId = ruleId,
            Protocol = WfpConstants.ProtocolTcp,
            Direction = RuleDirection.Outbound,
            RemoteIpAddress = 0x01010101,
            RemoteIpMask = 0xFFFFFFFF,
            RemotePort = 443,
        };
    }

    // ========================================
    // ApplyFilters: Empty to Non-Empty
    // ========================================

    [Fact]
    public void ApplyFilters_EmptyToSingle_CreatesOneFilter()
    {
        // Arrange
        var filters = new List<CompiledFilter> { CreateFilter("rule1") };

        // Act
        var result = _engine.ApplyFilters(filters);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.FiltersCreated);
        Assert.Equal(0, result.Value.FiltersRemoved);
        Assert.Equal(0, result.Value.FiltersUnchanged);
        Assert.Equal(1, _fake.FilterCount);
    }

    [Fact]
    public void ApplyFilters_EmptyToMultiple_CreatesAllFilters()
    {
        // Arrange
        var filters = new List<CompiledFilter>
        {
            CreateFilter("rule1"),
            CreateFilter("rule2"),
            CreateFilter("rule3")
        };

        // Act
        var result = _engine.ApplyFilters(filters);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.FiltersCreated);
        Assert.Equal(0, result.Value.FiltersRemoved);
        Assert.Equal(0, result.Value.FiltersUnchanged);
        Assert.Equal(3, _fake.FilterCount);
    }

    // ========================================
    // ApplyFilters: Non-Empty to Empty
    // ========================================

    [Fact]
    public void ApplyFilters_SingleToEmpty_RemovesOneFilter()
    {
        // Arrange
        var existingFilter = CreateFilter("rule1");
        _fake.AddExistingFilter(existingFilter.FilterKey, 100, "Existing Filter");

        // Act
        var result = _engine.ApplyFilters(new List<CompiledFilter>());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.FiltersCreated);
        Assert.Equal(1, result.Value.FiltersRemoved);
        Assert.Equal(0, result.Value.FiltersUnchanged);
        Assert.Equal(0, _fake.FilterCount);
    }

    [Fact]
    public void ApplyFilters_MultipleToEmpty_RemovesAllFilters()
    {
        // Arrange
        _fake.AddExistingFilter(CreateFilter("rule1").FilterKey, 100);
        _fake.AddExistingFilter(CreateFilter("rule2").FilterKey, 101);
        _fake.AddExistingFilter(CreateFilter("rule3").FilterKey, 102);

        // Act
        var result = _engine.ApplyFilters(new List<CompiledFilter>());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.FiltersCreated);
        Assert.Equal(3, result.Value.FiltersRemoved);
        Assert.Equal(0, result.Value.FiltersUnchanged);
        Assert.Equal(0, _fake.FilterCount);
    }

    // ========================================
    // ApplyFilters: Partial Overlap
    // ========================================

    [Fact]
    public void ApplyFilters_PartialOverlap_AddsAndRemoves()
    {
        // Arrange - existing: rule1, rule2
        _fake.AddExistingFilter(CreateFilter("rule1").FilterKey, 100);
        _fake.AddExistingFilter(CreateFilter("rule2").FilterKey, 101);

        // Desired: rule2, rule3 (remove rule1, add rule3, keep rule2)
        var filters = new List<CompiledFilter>
        {
            CreateFilter("rule2"),
            CreateFilter("rule3")
        };

        // Act
        var result = _engine.ApplyFilters(filters);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.FiltersCreated);   // rule3 added
        Assert.Equal(1, result.Value.FiltersRemoved);   // rule1 removed
        Assert.Equal(1, result.Value.FiltersUnchanged); // rule2 unchanged
        Assert.Equal(2, _fake.FilterCount);
    }

    [Fact]
    public void ApplyFilters_CompleteReplacement_RemovesAllAndAddsAll()
    {
        // Arrange - existing: rule1, rule2
        _fake.AddExistingFilter(CreateFilter("rule1").FilterKey, 100);
        _fake.AddExistingFilter(CreateFilter("rule2").FilterKey, 101);

        // Desired: rule3, rule4 (no overlap)
        var filters = new List<CompiledFilter>
        {
            CreateFilter("rule3"),
            CreateFilter("rule4")
        };

        // Act
        var result = _engine.ApplyFilters(filters);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.FiltersCreated);
        Assert.Equal(2, result.Value.FiltersRemoved);
        Assert.Equal(0, result.Value.FiltersUnchanged);
        Assert.Equal(2, _fake.FilterCount);
    }

    // ========================================
    // ApplyFilters: Idempotency
    // ========================================

    [Fact]
    public void ApplyFilters_SamePolicy_NoChanges()
    {
        // Arrange - apply initial policy
        var filters = new List<CompiledFilter>
        {
            CreateFilter("rule1"),
            CreateFilter("rule2")
        };
        _engine.ApplyFilters(filters);
        _fake.ResetCallTracking();

        // Act - apply same policy again
        var result = _engine.ApplyFilters(filters);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.FiltersCreated);
        Assert.Equal(0, result.Value.FiltersRemoved);
        Assert.Equal(2, result.Value.FiltersUnchanged);

        // Verify no add/delete calls were made (true idempotency)
        Assert.Empty(_fake.AddedFilterKeys);
        Assert.Empty(_fake.DeletedFilterKeys);
    }

    [Fact]
    public void ApplyFilters_EmptyToEmpty_NoChanges()
    {
        // Act - apply empty policy to empty state
        var result = _engine.ApplyFilters(new List<CompiledFilter>());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.FiltersCreated);
        Assert.Equal(0, result.Value.FiltersRemoved);
        Assert.Equal(0, result.Value.FiltersUnchanged);
    }

    // ========================================
    // ApplyFilters: Null Handling
    // ========================================

    [Fact]
    public void ApplyFilters_NullList_TreatedAsEmpty()
    {
        // Arrange - add existing filter
        _fake.AddExistingFilter(CreateFilter("rule1").FilterKey, 100);

        // Act
        var result = _engine.ApplyFilters(null!);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.FiltersCreated);
        Assert.Equal(1, result.Value.FiltersRemoved);
        Assert.Equal(0, _fake.FilterCount);
    }

    // ========================================
    // ApplyFilters: Error Handling
    // ========================================

    [Fact]
    public void ApplyFilters_EnumerationFailure_ReturnsError()
    {
        // Arrange
        _fake.EnumerateFiltersError = new Error(ErrorCodes.WfpError, "Enumeration failed");

        // Act
        var result = _engine.ApplyFilters(new List<CompiledFilter> { CreateFilter("rule1") });

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WfpError, result.Error.Code);
    }

    [Fact]
    public void ApplyFilters_AddFilterFailure_ReturnsError()
    {
        // Arrange
        _fake.AddFilterError = new Error(ErrorCodes.WfpError, "Add filter failed");

        // Act
        var result = _engine.ApplyFilters(new List<CompiledFilter> { CreateFilter("rule1") });

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WfpError, result.Error.Code);
    }

    [Fact]
    public void ApplyFilters_DeleteFilterFailure_ReturnsError()
    {
        // Arrange
        _fake.AddExistingFilter(CreateFilter("rule1").FilterKey, 100);
        _fake.DeleteFilterByKeyError = new Error(ErrorCodes.WfpError, "Delete filter failed");

        // Act - try to remove the existing filter
        var result = _engine.ApplyFilters(new List<CompiledFilter>());

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WfpError, result.Error.Code);
    }
}

/// <summary>
/// Unit tests for WfpEngine.RemoveAllFilters() rollback logic.
/// Verifies that rollback always deletes all filters in our sublayer.
/// </summary>
public class WfpEngineRollbackTests
{
    private readonly FakeWfpInterop _fake;
    private readonly FakeWfpNativeTransaction _fakeTransaction;
    private readonly WfpEngine _engine;

    public WfpEngineRollbackTests()
    {
        _fake = new FakeWfpInterop();
        _fakeTransaction = new FakeWfpNativeTransaction();
        _engine = new WfpEngine(NullLogger<WfpEngine>.Instance, _fake, _fakeTransaction);
    }

    // ========================================
    // Helper Methods
    // ========================================

    private static Guid GenerateFilterKey(string ruleId)
    {
        var input = $"{ruleId}:0";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x40);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash);
    }

    // ========================================
    // RemoveAllFilters: Basic Cases
    // ========================================

    [Fact]
    public void RemoveAllFilters_NoFilters_ReturnsZero()
    {
        // Act
        var result = _engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
        Assert.Equal(0, _fake.FilterCount);
    }

    [Fact]
    public void RemoveAllFilters_SingleFilter_RemovesAndReturnsOne()
    {
        // Arrange
        _fake.AddExistingFilter(GenerateFilterKey("rule1"), 100, "Filter 1");

        // Act
        var result = _engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value);
        Assert.Equal(0, _fake.FilterCount);
    }

    [Fact]
    public void RemoveAllFilters_MultipleFilters_RemovesAllAndReturnsCount()
    {
        // Arrange
        _fake.AddExistingFilter(GenerateFilterKey("rule1"), 100);
        _fake.AddExistingFilter(GenerateFilterKey("rule2"), 101);
        _fake.AddExistingFilter(GenerateFilterKey("rule3"), 102);
        _fake.AddExistingFilter(GenerateFilterKey("rule4"), 103);
        _fake.AddExistingFilter(GenerateFilterKey("rule5"), 104);

        // Act
        var result = _engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
        Assert.Equal(0, _fake.FilterCount);
    }

    // ========================================
    // RemoveAllFilters: Idempotency
    // ========================================

    [Fact]
    public void RemoveAllFilters_CalledTwice_SecondCallReturnsZero()
    {
        // Arrange
        _fake.AddExistingFilter(GenerateFilterKey("rule1"), 100);
        _fake.AddExistingFilter(GenerateFilterKey("rule2"), 101);

        // Act - first call
        var result1 = _engine.RemoveAllFilters();
        Assert.True(result1.IsSuccess);
        Assert.Equal(2, result1.Value);

        // Act - second call
        var result2 = _engine.RemoveAllFilters();
        Assert.True(result2.IsSuccess);
        Assert.Equal(0, result2.Value);

        // Assert
        Assert.Equal(0, _fake.FilterCount);
    }

    // ========================================
    // RemoveAllFilters: Error Handling
    // ========================================

    [Fact]
    public void RemoveAllFilters_EnumerationFailure_ReturnsError()
    {
        // Arrange
        _fake.AddExistingFilter(GenerateFilterKey("rule1"), 100);
        _fake.EnumerateFiltersError = new Error(ErrorCodes.WfpError, "Failed to enumerate");

        // Act
        var result = _engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WfpError, result.Error.Code);
        Assert.Contains("enumerate", result.Error.Message.ToLower());
    }

    [Fact]
    public void RemoveAllFilters_DeleteFailure_ReturnsError()
    {
        // Arrange
        _fake.AddExistingFilter(GenerateFilterKey("rule1"), 100);
        _fake.DeleteFilterByIdError = new Error(ErrorCodes.WfpError, "Failed to delete filter");

        // Act
        var result = _engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WfpError, result.Error.Code);
    }

    // ========================================
    // RemoveAllFilters: Verifies Correct Filters Deleted
    // ========================================

    [Fact]
    public void RemoveAllFilters_DeletesById_NotByKey()
    {
        // Arrange
        _fake.AddExistingFilter(GenerateFilterKey("rule1"), 100);
        _fake.AddExistingFilter(GenerateFilterKey("rule2"), 101);

        // Act
        var result = _engine.RemoveAllFilters();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, _fake.DeleteFilterByIdCallCount);
        Assert.Contains(100ul, _fake.DeletedFilterIds);
        Assert.Contains(101ul, _fake.DeletedFilterIds);

        // DeleteFilterByKey should not be called during RemoveAllFilters
        Assert.Equal(0, _fake.DeleteFilterByKeyCallCount);
    }

    // ========================================
    // RemoveAllFilters: Integration with ApplyFilters
    // ========================================

    [Fact]
    public void ApplyThenRollback_RemovesAllAppliedFilters()
    {
        // Arrange - apply a policy
        var filter1 = new CompiledFilter
        {
            FilterKey = GenerateFilterKey("rule1"),
            DisplayName = "Rule 1",
            RuleId = "rule1",
            Protocol = 6,
            Direction = RuleDirection.Outbound
        };
        var filter2 = new CompiledFilter
        {
            FilterKey = GenerateFilterKey("rule2"),
            DisplayName = "Rule 2",
            RuleId = "rule2",
            Protocol = 6,
            Direction = RuleDirection.Outbound
        };

        var applyResult = _engine.ApplyFilters(new List<CompiledFilter> { filter1, filter2 });
        Assert.True(applyResult.IsSuccess);
        Assert.Equal(2, _fake.FilterCount);

        // Act - rollback
        var rollbackResult = _engine.RemoveAllFilters();

        // Assert
        Assert.True(rollbackResult.IsSuccess);
        Assert.Equal(2, rollbackResult.Value);
        Assert.Equal(0, _fake.FilterCount);
    }

    [Fact]
    public void RollbackThenApply_AppliesCorrectly()
    {
        // Arrange - add some existing filters
        _fake.AddExistingFilter(GenerateFilterKey("old1"), 100);
        _fake.AddExistingFilter(GenerateFilterKey("old2"), 101);

        // Act - rollback first
        var rollbackResult = _engine.RemoveAllFilters();
        Assert.True(rollbackResult.IsSuccess);
        Assert.Equal(2, rollbackResult.Value);

        // Then apply new policy
        var newFilter = new CompiledFilter
        {
            FilterKey = GenerateFilterKey("new1"),
            DisplayName = "New Rule 1",
            RuleId = "new1",
            Protocol = 6,
            Direction = RuleDirection.Outbound
        };

        var applyResult = _engine.ApplyFilters(new List<CompiledFilter> { newFilter });

        // Assert
        Assert.True(applyResult.IsSuccess);
        Assert.Equal(1, applyResult.Value.FiltersCreated);
        Assert.Equal(0, applyResult.Value.FiltersRemoved); // Already removed by rollback
        Assert.Equal(1, _fake.FilterCount);
    }
}

/// <summary>
/// Tests for WfpEngine bootstrap operations using FakeWfpInterop.
/// </summary>
public class WfpEngineFakeBootstrapTests
{
    private readonly FakeWfpInterop _fake;
    private readonly FakeWfpNativeTransaction _fakeTransaction;
    private readonly WfpEngine _engine;

    public WfpEngineFakeBootstrapTests()
    {
        _fake = new FakeWfpInterop();
        _fakeTransaction = new FakeWfpNativeTransaction();
        _engine = new WfpEngine(NullLogger<WfpEngine>.Instance, _fake, _fakeTransaction);
    }

    [Fact]
    public void EnsureProviderAndSublayerExist_FromClean_CreatesBoth()
    {
        // Arrange
        _fake.ProviderExistsValue = false;
        _fake.SublayerExistsValue = false;

        // Act
        var result = _engine.EnsureProviderAndSublayerExist();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(_fake.ProviderExistsValue);
        Assert.True(_fake.SublayerExistsValue);
        Assert.Equal(1, _fake.AddProviderCallCount);
        Assert.Equal(1, _fake.AddSublayerCallCount);
    }

    [Fact]
    public void EnsureProviderAndSublayerExist_AlreadyExist_NoCreation()
    {
        // Arrange
        _fake.ProviderExistsValue = true;
        _fake.SublayerExistsValue = true;

        // Act
        var result = _engine.EnsureProviderAndSublayerExist();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, _fake.AddProviderCallCount);
        Assert.Equal(0, _fake.AddSublayerCallCount);
    }

    [Fact]
    public void EnsureProviderAndSublayerExist_ProviderFails_ReturnsError()
    {
        // Arrange
        _fake.ProviderExistsValue = false;
        _fake.AddProviderError = new Error(ErrorCodes.WfpError, "Provider creation failed");

        // Act
        var result = _engine.EnsureProviderAndSublayerExist();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.WfpError, result.Error.Code);
        Assert.Equal(0, _fake.AddSublayerCallCount); // Sublayer not attempted
    }

    [Fact]
    public void RemoveProviderAndSublayer_WithFilters_FailsWithInUse()
    {
        // Arrange
        _fake.ProviderExistsValue = true;
        _fake.SublayerExistsValue = true;
        _fake.AddExistingFilter(Guid.NewGuid(), 100); // Add a filter

        // Act
        var result = _engine.RemoveProviderAndSublayer();

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("filters still exist", result.Error.Message.ToLower());
    }

    [Fact]
    public void RemoveProviderAndSublayer_NoFilters_Succeeds()
    {
        // Arrange
        _fake.ProviderExistsValue = true;
        _fake.SublayerExistsValue = true;
        // No filters

        // Act
        var result = _engine.RemoveProviderAndSublayer();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(_fake.ProviderExistsValue);
        Assert.False(_fake.SublayerExistsValue);
    }
}
