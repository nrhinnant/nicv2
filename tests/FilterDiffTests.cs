using WfpTrafficControl.Shared.Policy;
using Xunit;

namespace WfpTrafficControl.Tests;

/// <summary>
/// Unit tests for FilterDiff computation logic.
/// Phase 13: Idempotent Reconciliation
/// </summary>
public class FilterDiffTests
{
    // ========================================
    // Empty Input Tests
    // ========================================

    [Fact]
    public void ComputeDiffBothEmptyReturnsEmptyDiff()
    {
        var desired = new List<CompiledFilter>();
        var current = new List<ExistingFilter>();

        var diff = FilterDiffComputer.ComputeDiff(desired, current);

        Assert.True(diff.IsEmpty);
        Assert.False(diff.HasChanges);
        Assert.Empty(diff.ToAdd);
        Assert.Empty(diff.ToRemove);
        Assert.Equal(0, diff.Unchanged);
        Assert.Equal(0, diff.FinalCount);
    }

    [Fact]
    public void ComputeDiffNullDesiredTreatsAsEmpty()
    {
        var current = new List<ExistingFilter>
        {
            CreateExistingFilter(Guid.NewGuid(), 1, "filter-1")
        };

        var diff = FilterDiffComputer.ComputeDiff(null, current);

        Assert.True(diff.HasChanges);
        Assert.Empty(diff.ToAdd);
        Assert.Single(diff.ToRemove);
        Assert.Equal(0, diff.Unchanged);
    }

    [Fact]
    public void ComputeDiffNullCurrentTreatsAsEmpty()
    {
        var desired = new List<CompiledFilter>
        {
            CreateCompiledFilter(Guid.NewGuid(), "rule-1")
        };

        var diff = FilterDiffComputer.ComputeDiff(desired, null);

        Assert.True(diff.HasChanges);
        Assert.Single(diff.ToAdd);
        Assert.Empty(diff.ToRemove);
        Assert.Equal(0, diff.Unchanged);
    }

    [Fact]
    public void ComputeDiffBothNullReturnsEmptyDiff()
    {
        var diff = FilterDiffComputer.ComputeDiff(null, null);

        Assert.True(diff.IsEmpty);
        Assert.Empty(diff.ToAdd);
        Assert.Empty(diff.ToRemove);
        Assert.Equal(0, diff.Unchanged);
    }

    // ========================================
    // Add-Only Tests
    // ========================================

    [Fact]
    public void ComputeDiffAllNewReturnsAllToAdd()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();

        var desired = new List<CompiledFilter>
        {
            CreateCompiledFilter(guid1, "rule-1"),
            CreateCompiledFilter(guid2, "rule-2")
        };
        var current = new List<ExistingFilter>();

        var diff = FilterDiffComputer.ComputeDiff(desired, current);

        Assert.True(diff.HasChanges);
        Assert.Equal(2, diff.ToAdd.Count);
        Assert.Empty(diff.ToRemove);
        Assert.Equal(0, diff.Unchanged);
        Assert.Equal(2, diff.FinalCount);
        Assert.Contains(diff.ToAdd, f => f.FilterKey == guid1);
        Assert.Contains(diff.ToAdd, f => f.FilterKey == guid2);
    }

    [Fact]
    public void ComputeDiffSomeNewReturnsOnlyNewToAdd()
    {
        var existingGuid = Guid.NewGuid();
        var newGuid = Guid.NewGuid();

        var desired = new List<CompiledFilter>
        {
            CreateCompiledFilter(existingGuid, "rule-existing"),
            CreateCompiledFilter(newGuid, "rule-new")
        };
        var current = new List<ExistingFilter>
        {
            CreateExistingFilter(existingGuid, 1, "existing-filter")
        };

        var diff = FilterDiffComputer.ComputeDiff(desired, current);

        Assert.True(diff.HasChanges);
        Assert.Single(diff.ToAdd);
        Assert.Equal(newGuid, diff.ToAdd[0].FilterKey);
        Assert.Empty(diff.ToRemove);
        Assert.Equal(1, diff.Unchanged);
        Assert.Equal(2, diff.FinalCount);
    }

    // ========================================
    // Remove-Only Tests
    // ========================================

    [Fact]
    public void ComputeDiffAllRemovedReturnsAllToRemove()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();

        var desired = new List<CompiledFilter>();
        var current = new List<ExistingFilter>
        {
            CreateExistingFilter(guid1, 1, "filter-1"),
            CreateExistingFilter(guid2, 2, "filter-2")
        };

        var diff = FilterDiffComputer.ComputeDiff(desired, current);

        Assert.True(diff.HasChanges);
        Assert.Empty(diff.ToAdd);
        Assert.Equal(2, diff.ToRemove.Count);
        Assert.Equal(0, diff.Unchanged);
        Assert.Equal(0, diff.FinalCount);
        Assert.Contains(guid1, diff.ToRemove);
        Assert.Contains(guid2, diff.ToRemove);
    }

    [Fact]
    public void ComputeDiffSomeRemovedReturnsOnlyObsoleteToRemove()
    {
        var keepGuid = Guid.NewGuid();
        var removeGuid = Guid.NewGuid();

        var desired = new List<CompiledFilter>
        {
            CreateCompiledFilter(keepGuid, "rule-keep")
        };
        var current = new List<ExistingFilter>
        {
            CreateExistingFilter(keepGuid, 1, "keep-filter"),
            CreateExistingFilter(removeGuid, 2, "remove-filter")
        };

        var diff = FilterDiffComputer.ComputeDiff(desired, current);

        Assert.True(diff.HasChanges);
        Assert.Empty(diff.ToAdd);
        Assert.Single(diff.ToRemove);
        Assert.Equal(removeGuid, diff.ToRemove[0]);
        Assert.Equal(1, diff.Unchanged);
        Assert.Equal(1, diff.FinalCount);
    }

    // ========================================
    // Unchanged (Idempotent) Tests
    // ========================================

    [Fact]
    public void ComputeDiffSameFiltersReturnsEmptyDiff()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();

        var desired = new List<CompiledFilter>
        {
            CreateCompiledFilter(guid1, "rule-1"),
            CreateCompiledFilter(guid2, "rule-2")
        };
        var current = new List<ExistingFilter>
        {
            CreateExistingFilter(guid1, 1, "filter-1"),
            CreateExistingFilter(guid2, 2, "filter-2")
        };

        var diff = FilterDiffComputer.ComputeDiff(desired, current);

        Assert.True(diff.IsEmpty);
        Assert.False(diff.HasChanges);
        Assert.Empty(diff.ToAdd);
        Assert.Empty(diff.ToRemove);
        Assert.Equal(2, diff.Unchanged);
        Assert.Equal(2, diff.FinalCount);
    }

    [Fact]
    public void ComputeDiffSameFiltersInDifferentOrderReturnsEmptyDiff()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var guid3 = Guid.NewGuid();

        var desired = new List<CompiledFilter>
        {
            CreateCompiledFilter(guid1, "rule-1"),
            CreateCompiledFilter(guid2, "rule-2"),
            CreateCompiledFilter(guid3, "rule-3")
        };
        // Current has filters in different order
        var current = new List<ExistingFilter>
        {
            CreateExistingFilter(guid3, 3, "filter-3"),
            CreateExistingFilter(guid1, 1, "filter-1"),
            CreateExistingFilter(guid2, 2, "filter-2")
        };

        var diff = FilterDiffComputer.ComputeDiff(desired, current);

        Assert.True(diff.IsEmpty);
        Assert.Equal(3, diff.Unchanged);
    }

    // ========================================
    // Mixed Add/Remove Tests
    // ========================================

    [Fact]
    public void ComputeDiffMixedAddRemoveReturnsBothLists()
    {
        var keepGuid = Guid.NewGuid();
        var addGuid = Guid.NewGuid();
        var removeGuid = Guid.NewGuid();

        var desired = new List<CompiledFilter>
        {
            CreateCompiledFilter(keepGuid, "rule-keep"),
            CreateCompiledFilter(addGuid, "rule-add")
        };
        var current = new List<ExistingFilter>
        {
            CreateExistingFilter(keepGuid, 1, "keep-filter"),
            CreateExistingFilter(removeGuid, 2, "remove-filter")
        };

        var diff = FilterDiffComputer.ComputeDiff(desired, current);

        Assert.True(diff.HasChanges);
        Assert.Single(diff.ToAdd);
        Assert.Equal(addGuid, diff.ToAdd[0].FilterKey);
        Assert.Single(diff.ToRemove);
        Assert.Equal(removeGuid, diff.ToRemove[0]);
        Assert.Equal(1, diff.Unchanged);
        Assert.Equal(2, diff.FinalCount);
    }

    [Fact]
    public void ComputeDiffCompleteReplacementReturnsAllToAddAndRemove()
    {
        var oldGuid1 = Guid.NewGuid();
        var oldGuid2 = Guid.NewGuid();
        var newGuid1 = Guid.NewGuid();
        var newGuid2 = Guid.NewGuid();

        var desired = new List<CompiledFilter>
        {
            CreateCompiledFilter(newGuid1, "new-rule-1"),
            CreateCompiledFilter(newGuid2, "new-rule-2")
        };
        var current = new List<ExistingFilter>
        {
            CreateExistingFilter(oldGuid1, 1, "old-filter-1"),
            CreateExistingFilter(oldGuid2, 2, "old-filter-2")
        };

        var diff = FilterDiffComputer.ComputeDiff(desired, current);

        Assert.True(diff.HasChanges);
        Assert.Equal(2, diff.ToAdd.Count);
        Assert.Equal(2, diff.ToRemove.Count);
        Assert.Equal(0, diff.Unchanged);
        Assert.Equal(2, diff.FinalCount);
    }

    // ========================================
    // Large Scale Tests
    // ========================================

    [Fact]
    public void ComputeDiffLargeNumberOfFiltersPerformsEfficiently()
    {
        // Test with a moderate number of filters to ensure HashSet usage is effective
        var sharedGuids = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();
        var addGuids = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();
        var removeGuids = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();

        var desired = sharedGuids.Concat(addGuids)
            .Select((g, i) => CreateCompiledFilter(g, $"rule-{i}"))
            .ToList();

        var current = sharedGuids.Concat(removeGuids)
            .Select((g, i) => CreateExistingFilter(g, (ulong)i, $"filter-{i}"))
            .ToList();

        var diff = FilterDiffComputer.ComputeDiff(desired, current);

        Assert.Equal(50, diff.ToAdd.Count);
        Assert.Equal(50, diff.ToRemove.Count);
        Assert.Equal(100, diff.Unchanged);
        Assert.Equal(150, diff.FinalCount);
    }

    // ========================================
    // FilterDiff Properties Tests
    // ========================================

    [Fact]
    public void FilterDiffIsEmptyTrueWhenBothListsEmpty()
    {
        var diff = new FilterDiff();
        Assert.True(diff.IsEmpty);
        Assert.False(diff.HasChanges);
    }

    [Fact]
    public void FilterDiffIsEmptyFalseWhenToAddHasItems()
    {
        var diff = new FilterDiff();
        diff.ToAdd.Add(CreateCompiledFilter(Guid.NewGuid(), "test"));

        Assert.False(diff.IsEmpty);
        Assert.True(diff.HasChanges);
    }

    [Fact]
    public void FilterDiffIsEmptyFalseWhenToRemoveHasItems()
    {
        var diff = new FilterDiff();
        diff.ToRemove.Add(Guid.NewGuid());

        Assert.False(diff.IsEmpty);
        Assert.True(diff.HasChanges);
    }

    [Fact]
    public void FilterDiffFinalCountCorrectWithUnchangedAndAdded()
    {
        var diff = new FilterDiff
        {
            Unchanged = 5
        };
        diff.ToAdd.Add(CreateCompiledFilter(Guid.NewGuid(), "test1"));
        diff.ToAdd.Add(CreateCompiledFilter(Guid.NewGuid(), "test2"));

        Assert.Equal(7, diff.FinalCount);
    }

    // ========================================
    // ExistingFilter Record Tests
    // ========================================

    [Fact]
    public void ExistingFilterEqualityBasedOnAllProperties()
    {
        var guid = Guid.NewGuid();
        var filter1 = new ExistingFilter { FilterKey = guid, FilterId = 123, DisplayName = "Test" };
        var filter2 = new ExistingFilter { FilterKey = guid, FilterId = 123, DisplayName = "Test" };
        var filter3 = new ExistingFilter { FilterKey = guid, FilterId = 456, DisplayName = "Test" };

        Assert.Equal(filter1, filter2);
        Assert.NotEqual(filter1, filter3);
    }

    // ========================================
    // Helper Methods
    // ========================================

    private static CompiledFilter CreateCompiledFilter(Guid filterKey, string ruleId)
    {
        return new CompiledFilter
        {
            FilterKey = filterKey,
            RuleId = ruleId,
            DisplayName = $"WfpTrafficControl: {ruleId}",
            Description = $"Test filter for {ruleId}",
            Action = FilterAction.Block,
            Weight = 1000,
            Protocol = 6 // TCP
        };
    }

    private static ExistingFilter CreateExistingFilter(Guid filterKey, ulong filterId, string displayName)
    {
        return new ExistingFilter
        {
            FilterKey = filterKey,
            FilterId = filterId,
            DisplayName = displayName
        };
    }
}
