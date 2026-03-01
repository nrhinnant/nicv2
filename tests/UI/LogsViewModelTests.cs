using WfpTrafficControl.UI.ViewModels;
using Xunit;

namespace WfpTrafficControl.Tests.UI;

/// <summary>
/// Unit tests for LogsViewModel.
/// </summary>
public class LogsViewModelTests
{
    private readonly MockServiceClient _mockService;
    private readonly MockDialogService _mockDialog;
    private readonly LogsViewModel _viewModel;

    public LogsViewModelTests()
    {
        _mockService = new MockServiceClient();
        _mockDialog = new MockDialogService();
        _viewModel = new LogsViewModel(_mockService, _mockDialog);
    }

    [Fact]
    public async Task InitializeAsyncCallsRefreshLogs()
    {
        // Arrange
        _mockService.ShouldConnect = true;

        // Act
        await _viewModel.InitializeAsync();

        // Assert
        Assert.Equal(1, _mockService.GetLogsCallCount);
    }

    [Fact]
    public async Task RefreshLogsAsyncLoadsEntriesWhenServiceOnline()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.LogEntryCount = 10;
        _mockService.LogTotalCount = 50;

        // Act
        await _viewModel.RefreshLogsAsync();

        // Assert
        Assert.Equal(10, _viewModel.LogEntries.Count);
        Assert.Equal(50, _viewModel.TotalLogCount);
        Assert.Contains("10 of 50", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshLogsAsyncWhenServiceOfflineShowsError()
    {
        // Arrange
        _mockService.ShouldConnect = false;

        // Act
        await _viewModel.RefreshLogsAsync();

        // Assert
        Assert.Empty(_viewModel.LogEntries);
        Assert.Contains("Failed", _viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshLogsAsyncUseTailFilterPassesTailParameter()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _viewModel.UseTailFilter = true;
        _viewModel.TailCount = 25;

        // Act
        await _viewModel.RefreshLogsAsync();

        // Assert
        Assert.Equal(25, _mockService.LastLogsTail);
        Assert.Null(_mockService.LastLogsSinceMinutes);
    }

    [Fact]
    public async Task RefreshLogsAsyncUseSinceFilterPassesSinceParameter()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _viewModel.UseTailFilter = false;
        _viewModel.SinceMinutes = 60;

        // Act
        await _viewModel.RefreshLogsAsync();

        // Assert
        Assert.Null(_mockService.LastLogsTail);
        Assert.Equal(60, _mockService.LastLogsSinceMinutes);
    }

    [Fact]
    public async Task RefreshLogsAsyncCapsTailAt500()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _viewModel.UseTailFilter = true;
        _viewModel.TailCount = 1000;

        // Act
        await _viewModel.RefreshLogsAsync();

        // Assert
        Assert.Equal(500, _mockService.LastLogsTail);
        Assert.Equal(500, _viewModel.TailCount); // Property should be updated too
    }

    [Fact]
    public async Task RefreshLogsAsyncSetsLogFilePath()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        _mockService.LogFilePath = @"C:\Custom\audit.log";

        // Act
        await _viewModel.RefreshLogsAsync();

        // Assert
        Assert.Equal(@"C:\Custom\audit.log", _viewModel.LogFilePath);
    }

    [Fact]
    public async Task RefreshLogsAsyncSetsIsLoadingDuringOperation()
    {
        // Arrange
        bool wasLoading = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(LogsViewModel.IsLoading) && _viewModel.IsLoading)
                wasLoading = true;
        };

        // Act
        await _viewModel.RefreshLogsAsync();

        // Assert
        Assert.True(wasLoading);
        Assert.False(_viewModel.IsLoading); // Should be false after completion
    }

    [Fact]
    public async Task ExportToCsvCommandWhenNoEntriesShowsWarning()
    {
        // Arrange - no logs loaded

        // Act
        await _viewModel.ExportToCsvCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, _mockDialog.WarningCount);
    }

    [Fact]
    public async Task ExportToCsvCommandWhenUserCancelsDoesNotExport()
    {
        // Arrange
        _mockService.ShouldConnect = true;
        await _viewModel.RefreshLogsAsync();
        _mockDialog.SaveFileResult = null; // User cancels

        // Act
        await _viewModel.ExportToCsvCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, _mockDialog.SuccessCount);
        Assert.Equal(0, _mockDialog.ErrorCount);
    }

    [Fact]
    public void ClearFilterCommandResetsToDefaults()
    {
        // Arrange
        _viewModel.UseTailFilter = false;
        _viewModel.TailCount = 100;
        _viewModel.SinceMinutes = 60;

        // Act
        _viewModel.ClearFilterCommand.Execute(null);

        // Assert
        Assert.True(_viewModel.UseTailFilter);
        Assert.Equal(50, _viewModel.TailCount);
        Assert.Equal(0, _viewModel.SinceMinutes);
    }

    [Fact]
    public void DefaultValuesAreCorrect()
    {
        // Assert
        Assert.True(_viewModel.UseTailFilter);
        Assert.Equal(50, _viewModel.TailCount);
        Assert.Equal(0, _viewModel.SinceMinutes);
        Assert.Empty(_viewModel.LogEntries);
        Assert.Equal("Click Refresh to load logs", _viewModel.StatusMessage);
    }
}
