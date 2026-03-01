using WfpTrafficControl.Shared.History;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Tests for PolicyHistoryViewModel.
/// </summary>
public class PolicyHistoryTests
{
    #region ViewModel Tests

    [Fact]
    public void ViewModel_InitialState_HasCorrectDefaults()
    {
        // Arrange
        var mockService = new MockServiceClient();
        var mockDialog = new MockDialogService();

        // Act
        var vm = new PolicyHistoryViewModel(mockService, mockDialog);

        // Assert
        Assert.Empty(vm.HistoryEntries);
        Assert.Equal(0, vm.TotalCount);
        Assert.Null(vm.SelectedEntry);
        Assert.False(vm.IsLoading);
        Assert.False(vm.CanRevert);
        Assert.Equal("Click Refresh to load history", vm.StatusMessage);
    }

    [Fact]
    public async Task RefreshHistory_LoadsEntries()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            HistoryEntries = new List<PolicyHistoryEntryDto>
            {
                new PolicyHistoryEntryDto
                {
                    Id = "test-1",
                    AppliedAt = DateTime.UtcNow,
                    PolicyVersion = "1.0.0",
                    RuleCount = 5,
                    Source = "CLI"
                },
                new PolicyHistoryEntryDto
                {
                    Id = "test-2",
                    AppliedAt = DateTime.UtcNow.AddHours(-1),
                    PolicyVersion = "0.9.0",
                    RuleCount = 3,
                    Source = "UI"
                }
            },
            HistoryTotalCount = 2
        };
        var mockDialog = new MockDialogService();
        var vm = new PolicyHistoryViewModel(mockService, mockDialog);

        // Act
        await vm.RefreshHistoryCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(2, vm.HistoryEntries.Count);
        Assert.Equal(2, vm.TotalCount);
        Assert.Equal("test-1", vm.HistoryEntries[0].Id);
        Assert.Equal("test-2", vm.HistoryEntries[1].Id);
        Assert.Equal(1, mockService.GetPolicyHistoryCallCount);
    }

    [Fact]
    public async Task RefreshHistory_EmptyResult_ShowsNoHistoryMessage()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            HistoryEntries = new List<PolicyHistoryEntryDto>(),
            HistoryTotalCount = 0
        };
        var mockDialog = new MockDialogService();
        var vm = new PolicyHistoryViewModel(mockService, mockDialog);

        // Act
        await vm.RefreshHistoryCommand.ExecuteAsync(null);

        // Assert
        Assert.Empty(vm.HistoryEntries);
        Assert.Equal(0, vm.TotalCount);
        Assert.Contains("No history entries", vm.StatusMessage);
    }

    [Fact]
    public async Task RefreshHistory_ServiceUnavailable_ShowsError()
    {
        // Arrange
        var mockService = new MockServiceClient
        {
            ShouldConnect = false
        };
        var mockDialog = new MockDialogService();
        var vm = new PolicyHistoryViewModel(mockService, mockDialog);

        // Act
        await vm.RefreshHistoryCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("Failed to load", vm.StatusMessage);
    }

    [Fact]
    public void SelectedEntry_Changes_UpdatesCanRevert()
    {
        // Arrange
        var mockService = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var vm = new PolicyHistoryViewModel(mockService, mockDialog);

        var entry = new PolicyHistoryEntryDto
        {
            Id = "test-1",
            AppliedAt = DateTime.UtcNow,
            PolicyVersion = "1.0.0",
            RuleCount = 5
        };

        // Act & Assert
        Assert.False(vm.CanRevert);

        vm.SelectedEntry = entry;
        Assert.True(vm.CanRevert);

        vm.SelectedEntry = null;
        Assert.False(vm.CanRevert);
    }

    [Fact]
    public async Task RevertToSelected_WithNoSelection_DoesNothing()
    {
        // Arrange
        var mockService = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var vm = new PolicyHistoryViewModel(mockService, mockDialog);
        vm.SelectedEntry = null;

        // Act
        await vm.RevertToSelectedCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, mockService.RevertToHistoryCallCount);
    }

    [Fact]
    public async Task ViewSelectedPolicy_LoadsPolicyContent()
    {
        // Arrange
        var mockService = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var vm = new PolicyHistoryViewModel(mockService, mockDialog);

        vm.SelectedEntry = new PolicyHistoryEntryDto
        {
            Id = "20250301-120000-001",
            PolicyVersion = "1.0.0"
        };

        // Act
        await vm.ViewSelectedPolicyCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, mockService.GetPolicyFromHistoryCallCount);
        Assert.Equal("20250301-120000-001", mockService.LastHistoryEntryId);
    }

    [Fact]
    public async Task ViewSelectedPolicy_WithNoSelection_DoesNothing()
    {
        // Arrange
        var mockService = new MockServiceClient();
        var mockDialog = new MockDialogService();
        var vm = new PolicyHistoryViewModel(mockService, mockDialog);
        vm.SelectedEntry = null;

        // Act
        await vm.ViewSelectedPolicyCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, mockService.GetPolicyFromHistoryCallCount);
    }

    #endregion

    #region Helper Method Tests

    [Theory]
    [InlineData("1.0.0", 5, "CLI", "v1.0.0 - 5 rules")]
    [InlineData("2.0.0", 10, "UI", "v2.0.0 - 10 rules")]
    [InlineData("", 0, "Watch", "v - 0 rules")]
    public void FormatEntryDisplay_FormatsCorrectly(string version, int ruleCount, string source, string expectedPrefix)
    {
        // Arrange
        var entry = new PolicyHistoryEntryDto
        {
            Id = "test",
            AppliedAt = DateTime.UtcNow,
            PolicyVersion = version,
            RuleCount = ruleCount,
            Source = source
        };

        // Act
        var result = PolicyHistoryViewModel.FormatEntryDisplay(entry);

        // Assert
        Assert.StartsWith(expectedPrefix, result);
        Assert.Contains(source, result);
    }

    #endregion

    #region PolicyHistoryEntryDto Tests

    [Fact]
    public void PolicyHistoryEntryDto_FromEntry_MapsAllFields()
    {
        // Arrange
        var entry = new PolicyHistoryEntry
        {
            Id = "test-id",
            AppliedAt = new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc),
            PolicyVersion = "1.0.0",
            RuleCount = 5,
            Source = "CLI",
            SourcePath = @"C:\path\to\policy.json",
            FiltersCreated = 3,
            FiltersRemoved = 2,
            FileName = "policy-test.json"
        };

        // Act
        var dto = PolicyHistoryEntryDto.FromEntry(entry);

        // Assert
        Assert.Equal("test-id", dto.Id);
        Assert.Equal(new DateTime(2025, 3, 1, 12, 0, 0, DateTimeKind.Utc), dto.AppliedAt);
        Assert.Equal("1.0.0", dto.PolicyVersion);
        Assert.Equal(5, dto.RuleCount);
        Assert.Equal("CLI", dto.Source);
        Assert.Equal(@"C:\path\to\policy.json", dto.SourcePath);
        Assert.Equal(3, dto.FiltersCreated);
        Assert.Equal(2, dto.FiltersRemoved);
    }

    #endregion

    #region MockServiceClient History Tests

    [Fact]
    public async Task MockServiceClient_GetPolicyHistory_ReturnsConfiguredEntries()
    {
        // Arrange
        var mock = new MockServiceClient();

        // Act
        var result = await mock.GetPolicyHistoryAsync();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Ok);
        Assert.Equal(mock.HistoryEntries.Count, result.Value.Entries.Count);
        Assert.Equal(mock.HistoryTotalCount, result.Value.TotalCount);
    }

    [Fact]
    public async Task MockServiceClient_RevertToHistory_ValidEntry_ReturnsSuccess()
    {
        // Arrange
        var mock = new MockServiceClient();
        var entryId = mock.HistoryEntries[0].Id;

        // Act
        var result = await mock.RevertToHistoryAsync(entryId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Ok);
        Assert.Equal(entryId, result.Value.RevertedToId);
    }

    [Fact]
    public async Task MockServiceClient_RevertToHistory_InvalidEntry_ReturnsNotFound()
    {
        // Arrange
        var mock = new MockServiceClient();

        // Act
        var result = await mock.RevertToHistoryAsync("non-existent-id");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Ok);
        Assert.Contains("not found", result.Value.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MockServiceClient_GetPolicyFromHistory_ValidEntry_ReturnsPolicyJson()
    {
        // Arrange
        var mock = new MockServiceClient();
        var entryId = mock.HistoryEntries[0].Id;

        // Act
        var result = await mock.GetPolicyFromHistoryAsync(entryId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Ok);
        Assert.NotNull(result.Value.PolicyJson);
        Assert.NotNull(result.Value.Entry);
    }

    [Fact]
    public async Task MockServiceClient_GetPolicyFromHistory_InvalidEntry_ReturnsNotFound()
    {
        // Arrange
        var mock = new MockServiceClient();

        // Act
        var result = await mock.GetPolicyFromHistoryAsync("non-existent-id");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Ok);
    }

    #endregion
}
