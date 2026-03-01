using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Unit tests for DashboardViewModel.
/// </summary>
public class DashboardViewModelTests
{
    private readonly MockServiceClient _mockService;
    private readonly MockDialogService _mockDialog;
    private readonly DashboardViewModel _viewModel;

    public DashboardViewModelTests()
    {
        _mockService = new MockServiceClient();
        _mockDialog = new MockDialogService();
        _viewModel = new DashboardViewModel(_mockService, _mockDialog);
    }

    [Fact]
    public async Task InitializeAsyncWhenServiceOnlineSetsConnectedState()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.ShouldSucceed = true;
        _mockService.ServiceVersion = "1.2.3";

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        Assert.True(_viewModel.IsConnected);
        Assert.Equal("Online", _viewModel.ServiceStatusText);
        Assert.Equal("1.2.3", _viewModel.ServiceVersion);
    }

    [Fact]
    public async Task InitializeAsyncWhenServiceOfflineSetsDisconnectedState()
    {
        // Arrange
        _mockService.ShouldConnect = false;

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        Assert.False(_viewModel.IsConnected);
        Assert.Contains("Offline", _viewModel.ServiceStatusText);
    }

    [Fact]
    public async Task RefreshStatusAsyncCallsPingAndLkgAndLogs()
    {
        // Arrange
        _mockService.ShouldConnect = true;

        // Act
        await _viewModel.RefreshStatusAsync();

        // Assert
        Assert.Equal(1, _mockService.PingCallCount);
        Assert.Equal(1, _mockService.GetLkgCallCount);
        Assert.Equal(1, _mockService.GetLogsCallCount);
    }

    [Fact]
    public async Task RefreshStatusAsyncWhenLkgExistsSetsLkgStatus()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.LkgExists = true;
        _mockService.LkgIsCorrupt = false;
        _mockService.LkgPolicyVersion = "2.0.0";
        _mockService.LkgRuleCount = 10;

        // Act
        await _viewModel.RefreshStatusAsync();

        // Assert
        Assert.True(_viewModel.HasLkg);
        Assert.Equal("Available", _viewModel.LkgStatusText);
        Assert.Equal("2.0.0", _viewModel.LkgPolicyVersion);
        Assert.Equal(10, _viewModel.LkgRuleCount);
    }

    [Fact]
    public async Task RefreshStatusAsyncWhenLkgCorruptSetsCorruptStatus()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.LkgExists = true;
        _mockService.LkgIsCorrupt = true;

        // Act
        await _viewModel.RefreshStatusAsync();

        // Assert
        Assert.False(_viewModel.HasLkg);
        Assert.Equal("Corrupt", _viewModel.LkgStatusText);
    }

    [Fact]
    public async Task ApplyPolicyCommandWhenUserCancelsDoesNotApply()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockDialog.OpenFileResult = @"C:\test\policy.json";
        _mockDialog.ConfirmResult = false; // User cancels

        // Act
        await _viewModel.ApplyPolicyCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, _mockService.ApplyCallCount);
    }

    [Fact]
    public async Task ApplyPolicyCommandWhenUserCancelsFileDialogDoesNotApply()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockDialog.OpenFileResult = null; // User cancels file dialog

        // Act
        await _viewModel.ApplyPolicyCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, _mockService.ApplyCallCount);
        Assert.Equal(0, _mockDialog.ConfirmCount);
    }

    [Fact]
    public async Task ApplyPolicyCommandWhenSuccessfulShowsSuccessDialog()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.ShouldSucceed = true;
        _mockService.FilterCount = 5;
        _mockDialog.ConfirmResult = true;

        // Act
        await _viewModel.ApplyPolicyCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _mockService.ApplyCallCount);
        Assert.Equal(1, _mockDialog.SuccessCount);
        Assert.Contains("5", _mockDialog.LastSuccessMessage); // Filter count
        Assert.Equal(5, _viewModel.FilterCount);
    }

    [Fact]
    public async Task ApplyPolicyCommandWhenFailsShowsErrorDialog()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.ShouldSucceed = false;
        _mockService.ErrorMessage = "Compilation failed";
        _mockDialog.ConfirmResult = true;

        // Act
        await _viewModel.ApplyPolicyCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _mockDialog.ErrorCount);
        Assert.Contains("Compilation failed", _mockDialog.LastErrorMessage);
    }

    [Fact]
    public async Task RollbackCommandWhenUserCancelsDoesNotRollback()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockDialog.ConfirmResult = false;

        // Act
        await _viewModel.RollbackCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, _mockService.RollbackCallCount);
    }

    [Fact]
    public async Task RollbackCommandWhenSuccessfulShowsSuccessMessage()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.ShouldSucceed = true;
        _mockService.FilterCount = 10;
        _mockDialog.ConfirmResult = true;

        // Act
        await _viewModel.RollbackCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _mockService.RollbackCallCount);
        Assert.Equal(1, _mockDialog.SuccessCount);
        // Success message should contain filters removed count
        Assert.Contains("10", _mockDialog.LastSuccessMessage!);
        Assert.Contains("Rollback", _mockDialog.LastSuccessMessage!);
    }

    [Fact]
    public async Task RollbackCommandRequiresWarningConfirmation()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockDialog.ConfirmResult = true;

        // Act
        await _viewModel.RollbackCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _mockDialog.ConfirmWarningCount);
        Assert.Contains("ALL", _mockDialog.LastConfirmMessage);
    }

    [Fact]
    public async Task RevertToLkgCommandWhenNoLkgShowsWarning()
    {
        // Arrange
        _viewModel.GetType().GetProperty("HasLkg")?.SetValue(_viewModel, false);

        // Act
        await _viewModel.RevertToLkgCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, _mockService.RevertToLkgCallCount);
        Assert.Equal(1, _mockDialog.WarningCount);
    }

    [Fact]
    public async Task RevertToLkgCommandWhenSuccessfulUpdatesState()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.ShouldSucceed = true;
        _mockService.LkgRuleCount = 7;
        _mockDialog.ConfirmResult = true;

        // First refresh to set HasLkg
        await _viewModel.RefreshStatusAsync();

        // Act
        await _viewModel.RevertToLkgCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _mockService.RevertToLkgCallCount);
        Assert.Equal(1, _mockDialog.SuccessCount);
        Assert.True(_viewModel.HasPolicyApplied);
        // Success message should mention filters created from LKG
        Assert.Contains("7", _mockDialog.LastSuccessMessage!);
    }

    [Fact]
    public async Task StatusUpdatedRaisedOnRefresh()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.ServiceVersion = "1.0.0";
        DashboardStatusEventArgs? receivedArgs = null;
        _viewModel.StatusUpdated += (s, e) => receivedArgs = e;

        // Act
        await _viewModel.RefreshStatusAsync();

        // Assert
        Assert.NotNull(receivedArgs);
        Assert.True(receivedArgs.IsConnected);
        Assert.Equal("1.0.0", receivedArgs.ServiceVersion);
    }

    [Fact]
    public async Task IsLoadingSetDuringRefresh()
    {
        // Arrange
        bool wasLoading = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DashboardViewModel.IsLoading) && _viewModel.IsLoading)
                wasLoading = true;
        };

        // Act
        await _viewModel.RefreshStatusAsync();

        // Assert
        Assert.True(wasLoading);
        Assert.False(_viewModel.IsLoading); // Should be false after completion
    }

    // ===== Hot Reload (Watch) Tests =====

    [Fact]
    public async Task RefreshStatusAsyncCallsWatchStatus()
    {
        // Arrange
        _mockService.ShouldConnect = true;

        // Act
        await _viewModel.RefreshStatusAsync();

        // Assert
        Assert.Equal(1, _mockService.WatchStatusCallCount);
    }

    [Fact]
    public async Task RefreshStatusAsyncWhenWatchingUpdatesWatchStatus()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.WatchIsWatching = true;
        _mockService.WatchPolicyPath = @"C:\test\policy.json";
        _mockService.WatchApplyCount = 5;
        _mockService.WatchErrorCount = 1;

        // Act
        await _viewModel.RefreshStatusAsync();

        // Assert
        Assert.True(_viewModel.IsWatching);
        Assert.Equal(@"C:\test\policy.json", _viewModel.WatchedFilePath);
        Assert.Equal(5, _viewModel.WatchApplyCount);
        Assert.Equal(1, _viewModel.WatchErrorCount);
    }

    [Fact]
    public async Task EnableWatchCommandWhenUserCancelsDoesNotEnable()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockDialog.OpenFileResult = null; // User cancels

        // Act
        await _viewModel.EnableWatchCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, _mockService.WatchSetCallCount);
    }

    [Fact]
    public async Task EnableWatchCommandWhenSuccessfulShowsSuccessDialog()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.ShouldSucceed = true;
        _mockDialog.OpenFileResult = @"C:\test\policy.json";

        // Act
        await _viewModel.EnableWatchCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _mockService.WatchSetCallCount);
        Assert.Equal(@"C:\test\policy.json", _mockService.LastWatchSetPath);
        Assert.Equal(1, _mockDialog.SuccessCount);
        Assert.True(_viewModel.IsWatching);
    }

    [Fact]
    public async Task DisableWatchCommandWhenNotWatchingDoesNothing()
    {
        // Arrange
        _viewModel.GetType().GetProperty("IsWatching")?.SetValue(_viewModel, false);

        // Act
        await _viewModel.DisableWatchCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, _mockService.WatchSetCallCount);
    }

    [Fact]
    public async Task DisableWatchCommandWhenUserCancelsDoesNotDisable()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.WatchIsWatching = true;
        _mockService.WatchPolicyPath = @"C:\test\policy.json";
        await _viewModel.RefreshStatusAsync(); // Set IsWatching to true
        _mockDialog.ConfirmResult = false; // User cancels

        // Act
        await _viewModel.DisableWatchCommand.ExecuteAsync(null);

        // Assert - WatchSetCallCount should still be 0 (only called during enable, not disable cancel)
        Assert.True(_viewModel.IsWatching); // Should still be watching
    }

    [Fact]
    public async Task DisableWatchCommandWhenSuccessfulShowsSuccessDialog()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.ShouldSucceed = true;
        _mockService.WatchIsWatching = true;
        _mockService.WatchPolicyPath = @"C:\test\policy.json";
        await _viewModel.RefreshStatusAsync(); // Set IsWatching to true
        _mockDialog.ConfirmResult = true;

        // Act
        await _viewModel.DisableWatchCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _mockService.WatchSetCallCount);
        Assert.Null(_mockService.LastWatchSetPath); // Should pass null to disable
        Assert.Equal(1, _mockDialog.SuccessCount);
        Assert.False(_viewModel.IsWatching);
    }
}
