using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for the Dashboard screen.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;
    private readonly IDialogService _dialogService;

    /// <summary>
    /// Event raised when status is updated.
    /// </summary>
    public event EventHandler<DashboardStatusEventArgs>? StatusUpdated;

    // Service Status
    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _serviceVersion = "Unknown";

    [ObservableProperty]
    private string _serviceStatusText = "Checking...";

    // Filter Status
    [ObservableProperty]
    private int _filterCount;

    // Policy Status
    [ObservableProperty]
    private string _policyVersion = "No policy applied";

    [ObservableProperty]
    private bool _hasPolicyApplied;

    // LKG Status
    [ObservableProperty]
    private bool _hasLkg;

    [ObservableProperty]
    private string _lkgStatusText = "Checking...";

    [ObservableProperty]
    private string _lkgPolicyVersion = "";

    [ObservableProperty]
    private int _lkgRuleCount;

    // Hot Reload (File Watch) Status
    [ObservableProperty]
    private bool _isWatching;

    [ObservableProperty]
    private string _watchStatusText = "Not watching";

    [ObservableProperty]
    private string _watchedFilePath = "";

    [ObservableProperty]
    private int _watchApplyCount;

    [ObservableProperty]
    private int _watchErrorCount;

    [ObservableProperty]
    private string _watchLastError = "";

    [ObservableProperty]
    private bool _isSettingWatch;

    // Loading States
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private bool _isRollingBack;

    [ObservableProperty]
    private bool _isRevertingToLkg;

    // Recent Activity
    [ObservableProperty]
    private ObservableCollection<AuditLogEntryDto> _recentActivity = new();

    // Last operation time for status bar
    private DateTime? _lastOperationTime;

    public DashboardViewModel(IServiceClient serviceClient, IDialogService dialogService)
    {
        _serviceClient = serviceClient;
        _dialogService = dialogService;
    }

    /// <summary>
    /// Initializes the dashboard by refreshing all status.
    /// </summary>
    [RelayCommand]
    public async Task InitializeAsync()
    {
        await RefreshStatusAsync();
    }

    /// <summary>
    /// Refreshes all status information from the service.
    /// </summary>
    [RelayCommand]
    public async Task RefreshStatusAsync()
    {
        IsLoading = true;

        try
        {
            // Ping service
            var pingResult = await _serviceClient.PingAsync();

            if (pingResult.IsSuccess && pingResult.Value.Ok)
            {
                IsConnected = true;
                ServiceVersion = pingResult.Value.ServiceVersion ?? "Unknown";
                ServiceStatusText = "Online";

                // Get LKG status
                await RefreshLkgStatusAsync();

                // Get watch status
                await RefreshWatchStatusAsync();

                // Get recent audit logs
                await RefreshRecentActivityAsync();
            }
            else
            {
                IsConnected = false;
                ServiceStatusText = "Offline";
                ServiceVersion = "N/A";

                if (pingResult.IsFailure)
                {
                    ServiceStatusText = $"Offline - {pingResult.Error.Message}";
                }
            }

            RaiseStatusUpdated();
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ServiceStatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshLkgStatusAsync()
    {
        var lkgResult = await _serviceClient.GetLkgAsync();

        if (lkgResult.IsSuccess && lkgResult.Value.Ok)
        {
            var lkg = lkgResult.Value;
            HasLkg = lkg.Exists && !lkg.IsCorrupt;

            if (lkg.Exists && !lkg.IsCorrupt)
            {
                LkgStatusText = "Available";
                LkgPolicyVersion = lkg.PolicyVersion ?? "Unknown";
                LkgRuleCount = lkg.RuleCount;
            }
            else if (lkg.IsCorrupt)
            {
                LkgStatusText = "Corrupt";
                HasLkg = false;
            }
            else
            {
                LkgStatusText = "Not available";
            }
        }
        else
        {
            HasLkg = false;
            LkgStatusText = "Unknown";
        }
    }

    private async Task RefreshWatchStatusAsync()
    {
        var watchResult = await _serviceClient.WatchStatusAsync();

        if (watchResult.IsSuccess && watchResult.Value.Ok)
        {
            var status = watchResult.Value;
            IsWatching = status.Watching;
            WatchedFilePath = status.PolicyPath ?? "";
            WatchApplyCount = status.ApplyCount;
            WatchErrorCount = status.ErrorCount;
            WatchLastError = status.LastError ?? "";

            if (status.Watching)
            {
                WatchStatusText = $"Watching ({status.ApplyCount} applies, {status.ErrorCount} errors)";
            }
            else
            {
                WatchStatusText = "Not watching";
            }
        }
        else
        {
            IsWatching = false;
            WatchStatusText = "Unknown";
        }
    }

    /// <summary>
    /// Enables file watching for hot reload.
    /// </summary>
    [RelayCommand]
    private async Task EnableWatchAsync()
    {
        var filePath = _dialogService.ShowOpenFileDialog(
            "JSON files (*.json)|*.json|All files (*.*)|*.*",
            "Select Policy File to Watch");

        if (string.IsNullOrEmpty(filePath))
            return;

        IsSettingWatch = true;

        try
        {
            var result = await _serviceClient.WatchSetAsync(filePath);

            if (result.IsSuccess && result.Value.Ok)
            {
                var response = result.Value;
                IsWatching = response.Watching;
                WatchedFilePath = response.PolicyPath ?? "";

                if (response.Watching)
                {
                    var message = $"Hot reload enabled!\n\nWatching: {response.PolicyPath}";
                    if (!response.InitialApplySuccess)
                    {
                        message += "\n\nWarning: Initial policy apply failed. Fix the policy file and save to trigger reload.";
                    }
                    if (!string.IsNullOrEmpty(response.Warning))
                    {
                        message += $"\n\nWarning: {response.Warning}";
                    }

                    _dialogService.ShowSuccess(message, "Hot Reload Enabled");
                    await RefreshWatchStatusAsync();
                    await RefreshRecentActivityAsync();
                    RaiseStatusUpdated();
                }
            }
            else
            {
                var errorMsg = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                _dialogService.ShowError($"Failed to enable hot reload:\n\n{errorMsg}", "Hot Reload Failed");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error enabling hot reload:\n\n{ex.Message}", "Error");
        }
        finally
        {
            IsSettingWatch = false;
        }
    }

    /// <summary>
    /// Disables file watching.
    /// </summary>
    [RelayCommand]
    private async Task DisableWatchAsync()
    {
        if (!IsWatching)
            return;

        if (!_dialogService.Confirm(
                $"Disable hot reload?\n\nCurrently watching:\n{WatchedFilePath}",
                "Confirm Disable"))
        {
            return;
        }

        IsSettingWatch = true;

        try
        {
            var result = await _serviceClient.WatchSetAsync(null);

            if (result.IsSuccess && result.Value.Ok)
            {
                IsWatching = false;
                WatchedFilePath = "";
                WatchStatusText = "Not watching";

                _dialogService.ShowSuccess("Hot reload disabled.", "Hot Reload Disabled");
                RaiseStatusUpdated();
            }
            else
            {
                var errorMsg = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                _dialogService.ShowError($"Failed to disable hot reload:\n\n{errorMsg}", "Error");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error disabling hot reload:\n\n{ex.Message}", "Error");
        }
        finally
        {
            IsSettingWatch = false;
        }
    }

    private async Task RefreshRecentActivityAsync()
    {
        var logsResult = await _serviceClient.GetLogsAsync(tail: 5);

        RecentActivity.Clear();

        if (logsResult.IsSuccess && logsResult.Value.Ok)
        {
            foreach (var entry in logsResult.Value.Entries)
            {
                RecentActivity.Add(entry);
            }

            // Update last operation time from most recent entry
            if (logsResult.Value.Entries.Count > 0)
            {
                var mostRecent = logsResult.Value.Entries[0];
                if (DateTime.TryParse(mostRecent.Timestamp, out var timestamp))
                {
                    _lastOperationTime = timestamp;
                }

                // Update filter count from most recent apply event
                var lastApply = logsResult.Value.Entries.FirstOrDefault(e =>
                    e.Event == "apply-finished" && e.Status == "success");
                if (lastApply != null)
                {
                    FilterCount = lastApply.FiltersCreated;
                    PolicyVersion = lastApply.PolicyVersion ?? "Unknown";
                    HasPolicyApplied = true;
                }
            }
        }
    }

    /// <summary>
    /// Opens a file dialog and applies the selected policy.
    /// </summary>
    [RelayCommand]
    private async Task ApplyPolicyAsync()
    {
        var filePath = _dialogService.ShowOpenFileDialog(
            "JSON files (*.json)|*.json|All files (*.*)|*.*",
            "Select Policy File");

        if (string.IsNullOrEmpty(filePath))
            return;

        if (!_dialogService.Confirm(
                $"Apply policy from:\n{filePath}\n\nThis will update the active firewall rules.",
                "Confirm Apply"))
        {
            return;
        }

        IsApplying = true;

        try
        {
            var result = await _serviceClient.ApplyAsync(filePath);

            if (result.IsSuccess && result.Value.Ok)
            {
                var response = result.Value;
                FilterCount = response.FiltersCreated;
                PolicyVersion = response.PolicyVersion ?? "Unknown";
                HasPolicyApplied = true;
                _lastOperationTime = DateTime.UtcNow;

                _dialogService.ShowSuccess(
                    $"Policy applied successfully!\n\n" +
                    $"Filters created: {response.FiltersCreated}\n" +
                    $"Filters removed: {response.FiltersRemoved}\n" +
                    $"Rules skipped: {response.RulesSkipped}\n" +
                    $"Total rules: {response.TotalRules}",
                    "Apply Successful");

                await RefreshRecentActivityAsync();
                await RefreshLkgStatusAsync();
                RaiseStatusUpdated();
            }
            else
            {
                var errorMsg = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";

                // Check for compilation errors
                if (result.IsSuccess && result.Value.CompilationErrors.Count > 0)
                {
                    var errors = string.Join("\n",
                        result.Value.CompilationErrors.Select(e => $"  - [{e.RuleId}] {e.Message}"));
                    errorMsg = $"Compilation errors:\n{errors}";
                }

                _dialogService.ShowError($"Failed to apply policy:\n\n{errorMsg}", "Apply Failed");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error applying policy:\n\n{ex.Message}", "Error");
        }
        finally
        {
            IsApplying = false;
        }
    }

    /// <summary>
    /// Executes a rollback to remove all filters.
    /// </summary>
    [RelayCommand]
    private async Task RollbackAsync()
    {
        if (!_dialogService.ConfirmWarning(
                "This will remove ALL active firewall filters.\n\n" +
                "All traffic will be unfiltered until a new policy is applied.\n\n" +
                "Are you sure you want to continue?",
                "Confirm Rollback"))
        {
            return;
        }

        IsRollingBack = true;

        try
        {
            var result = await _serviceClient.RollbackAsync();

            if (result.IsSuccess && result.Value.Ok)
            {
                FilterCount = 0;
                PolicyVersion = "No policy applied";
                HasPolicyApplied = false;
                _lastOperationTime = DateTime.UtcNow;

                _dialogService.ShowSuccess(
                    $"Rollback completed successfully!\n\n" +
                    $"Filters removed: {result.Value.FiltersRemoved}",
                    "Rollback Successful");

                await RefreshRecentActivityAsync();
                RaiseStatusUpdated();
            }
            else
            {
                var errorMsg = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                _dialogService.ShowError($"Failed to rollback:\n\n{errorMsg}", "Rollback Failed");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error during rollback:\n\n{ex.Message}", "Error");
        }
        finally
        {
            IsRollingBack = false;
        }
    }

    /// <summary>
    /// Reverts to the Last Known Good policy.
    /// </summary>
    [RelayCommand]
    private async Task RevertToLkgAsync()
    {
        if (!HasLkg)
        {
            _dialogService.ShowWarning(
                "No Last Known Good policy is available.\n\n" +
                "Apply a policy first to create a backup.",
                "No LKG Available");
            return;
        }

        if (!_dialogService.Confirm(
                $"Revert to Last Known Good policy?\n\n" +
                $"Version: {LkgPolicyVersion}\n" +
                $"Rules: {LkgRuleCount}\n\n" +
                "This will replace the current active policy.",
                "Confirm Revert to LKG"))
        {
            return;
        }

        IsRevertingToLkg = true;

        try
        {
            var result = await _serviceClient.RevertToLkgAsync();

            if (result.IsSuccess && result.Value.Ok)
            {
                var response = result.Value;
                FilterCount = response.FiltersCreated;
                PolicyVersion = response.PolicyVersion ?? "Unknown";
                HasPolicyApplied = true;
                _lastOperationTime = DateTime.UtcNow;

                _dialogService.ShowSuccess(
                    $"Reverted to LKG successfully!\n\n" +
                    $"Policy version: {response.PolicyVersion}\n" +
                    $"Filters created: {response.FiltersCreated}\n" +
                    $"Filters removed: {response.FiltersRemoved}\n" +
                    $"Total rules: {response.TotalRules}",
                    "LKG Restore Successful");

                await RefreshRecentActivityAsync();
                RaiseStatusUpdated();
            }
            else
            {
                var errorMsg = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                _dialogService.ShowError($"Failed to revert to LKG:\n\n{errorMsg}", "LKG Restore Failed");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Error reverting to LKG:\n\n{ex.Message}", "Error");
        }
        finally
        {
            IsRevertingToLkg = false;
        }
    }

    private void RaiseStatusUpdated()
    {
        StatusUpdated?.Invoke(this, new DashboardStatusEventArgs
        {
            IsConnected = IsConnected,
            ServiceVersion = ServiceVersion,
            FilterCount = FilterCount,
            LastOperationTime = _lastOperationTime
        });
    }
}
