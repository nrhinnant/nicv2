using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for displaying policy version history.
/// </summary>
public partial class PolicyHistoryViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;
    private readonly IDialogService _dialogService;

    /// <summary>
    /// Collection of history entries (most recent first).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PolicyHistoryEntryDto> _historyEntries = new();

    /// <summary>
    /// Total count of history entries available.
    /// </summary>
    [ObservableProperty]
    private int _totalCount;

    /// <summary>
    /// Currently selected history entry.
    /// </summary>
    [ObservableProperty]
    private PolicyHistoryEntryDto? _selectedEntry;

    /// <summary>
    /// Whether data is currently loading.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Status message for user feedback.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Click Refresh to load history";

    /// <summary>
    /// Whether the selected entry can be reverted to.
    /// </summary>
    public bool CanRevert => SelectedEntry != null && !IsLoading;

    public PolicyHistoryViewModel(IServiceClient serviceClient, IDialogService dialogService)
    {
        _serviceClient = serviceClient;
        _dialogService = dialogService;
    }

    partial void OnSelectedEntryChanged(PolicyHistoryEntryDto? value)
    {
        OnPropertyChanged(nameof(CanRevert));
    }

    /// <summary>
    /// Loads the policy history from the service.
    /// </summary>
    [RelayCommand]
    public async Task RefreshHistoryAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading history...";
        HistoryEntries.Clear();

        try
        {
            var result = await _serviceClient.GetPolicyHistoryAsync(100);

            if (result.IsSuccess && result.Value.Ok)
            {
                var response = result.Value;
                TotalCount = response.TotalCount;

                foreach (var entry in response.Entries)
                {
                    HistoryEntries.Add(entry);
                }

                StatusMessage = HistoryEntries.Count == 0
                    ? "No history entries found. Apply a policy to start tracking history."
                    : $"Showing {HistoryEntries.Count} of {TotalCount} entries";
            }
            else
            {
                var error = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                StatusMessage = $"Failed to load history: {error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Reverts to the selected history entry.
    /// </summary>
    [RelayCommand]
    public async Task RevertToSelectedAsync()
    {
        if (SelectedEntry == null)
            return;

        var confirm = MessageBox.Show(
            $"Are you sure you want to revert to version {SelectedEntry.PolicyVersion} " +
            $"from {SelectedEntry.AppliedAt:g}?\n\n" +
            "This will replace the currently active policy.",
            "Confirm Revert",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        IsLoading = true;
        StatusMessage = "Reverting to selected version...";

        try
        {
            var result = await _serviceClient.RevertToHistoryAsync(SelectedEntry.Id);

            if (result.IsSuccess && result.Value.Ok)
            {
                var response = result.Value;
                StatusMessage = $"Reverted to version {response.PolicyVersion}: " +
                               $"{response.FiltersCreated} filters created, " +
                               $"{response.FiltersRemoved} filters removed";

                MessageBox.Show(
                    $"Successfully reverted to policy version {response.PolicyVersion}.\n\n" +
                    $"Filters Created: {response.FiltersCreated}\n" +
                    $"Filters Removed: {response.FiltersRemoved}\n" +
                    $"Total Rules: {response.TotalRules}",
                    "Revert Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                var error = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                StatusMessage = $"Revert failed: {error}";

                MessageBox.Show(
                    $"Failed to revert to selected version:\n\n{error}",
                    "Revert Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show(
                $"Error during revert:\n\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Views the policy content for the selected history entry.
    /// </summary>
    [RelayCommand]
    public async Task ViewSelectedPolicyAsync()
    {
        if (SelectedEntry == null)
            return;

        IsLoading = true;
        StatusMessage = "Loading policy content...";

        try
        {
            var result = await _serviceClient.GetPolicyFromHistoryAsync(SelectedEntry.Id);

            if (result.IsSuccess && result.Value.Ok)
            {
                var response = result.Value;

                // Show policy JSON in a dialog
                var policyJson = response.PolicyJson ?? "{}";

                // Format for display
                try
                {
                    var formatted = System.Text.Json.JsonSerializer.Serialize(
                        System.Text.Json.JsonDocument.Parse(policyJson).RootElement,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    policyJson = formatted;
                }
                catch
                {
                    // Keep original if formatting fails
                }

                _dialogService.ShowInfo(
                    $"Policy Version {SelectedEntry.PolicyVersion}:\n\n{policyJson}",
                    "Policy Content");

                StatusMessage = $"Loaded policy version {SelectedEntry.PolicyVersion}";
            }
            else
            {
                var error = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                StatusMessage = $"Failed to load policy: {error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Formats a history entry for display in the list.
    /// </summary>
    public static string FormatEntryDisplay(PolicyHistoryEntryDto entry)
    {
        var timeAgo = FormatTimeAgo(entry.AppliedAt);
        return $"v{entry.PolicyVersion} - {entry.RuleCount} rules - {timeAgo} ({entry.Source})";
    }

    private static string FormatTimeAgo(DateTime utcTime)
    {
        var elapsed = DateTime.UtcNow - utcTime;

        if (elapsed.TotalMinutes < 1)
            return "just now";
        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours}h ago";
        if (elapsed.TotalDays < 7)
            return $"{(int)elapsed.TotalDays}d ago";
        return utcTime.ToLocalTime().ToString("g");
    }
}
