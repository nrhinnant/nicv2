using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WfpTrafficControl.Shared.Ipc;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for the Logs tab providing full audit log viewing.
/// </summary>
public partial class LogsViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;
    private readonly IDialogService _dialogService;

    // Filter options
    [ObservableProperty]
    private int _tailCount = 50;

    [ObservableProperty]
    private int _sinceMinutes;

    [ObservableProperty]
    private bool _useTailFilter = true;

    // Log data
    [ObservableProperty]
    private ObservableCollection<AuditLogEntryDto> _logEntries = new();

    [ObservableProperty]
    private int _totalLogCount;

    [ObservableProperty]
    private string _logFilePath = "";

    // Loading state
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Click Refresh to load logs";

    // Selected entry for details
    [ObservableProperty]
    private AuditLogEntryDto? _selectedEntry;

    public LogsViewModel(IServiceClient serviceClient, IDialogService dialogService)
    {
        _serviceClient = serviceClient;
        _dialogService = dialogService;
    }

    /// <summary>
    /// Initializes the logs view by loading initial data.
    /// </summary>
    [RelayCommand]
    public async Task InitializeAsync()
    {
        await RefreshLogsAsync();
    }

    /// <summary>
    /// Refreshes the log entries based on current filter settings.
    /// </summary>
    [RelayCommand]
    public async Task RefreshLogsAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading logs...";

        try
        {
            int? tail = UseTailFilter ? TailCount : null;
            int? since = UseTailFilter ? null : SinceMinutes;

            // Cap tail to reasonable maximum
            if (tail.HasValue && tail.Value > 500)
            {
                tail = 500;
                TailCount = 500;
            }

            var result = await _serviceClient.GetLogsAsync(tail: tail, sinceMinutes: since);

            LogEntries.Clear();

            if (result.IsSuccess && result.Value.Ok)
            {
                var response = result.Value;
                TotalLogCount = response.TotalCount;
                LogFilePath = response.LogPath ?? "";

                foreach (var entry in response.Entries)
                {
                    LogEntries.Add(entry);
                }

                StatusMessage = $"Showing {response.Count} of {response.TotalCount} entries";
            }
            else
            {
                var errorMsg = result.IsFailure
                    ? result.Error.Message
                    : result.Value.Error ?? "Unknown error";
                StatusMessage = $"Failed to load logs: {errorMsg}";
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
    /// Exports the current log entries to a CSV file.
    /// </summary>
    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        if (LogEntries.Count == 0)
        {
            _dialogService.ShowWarning("No log entries to export.", "Export");
            return;
        }

        var filePath = _dialogService.ShowSaveFileDialog(
            "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            $"audit-log-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            "Export Audit Log");

        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            var lines = new List<string>
            {
                "Timestamp,Event,Source,Status,PolicyVersion,FiltersCreated,FiltersRemoved,ErrorCode,ErrorMessage"
            };

            foreach (var entry in LogEntries)
            {
                var line = string.Join(",",
                    CsvEscape(entry.Timestamp),
                    CsvEscape(entry.Event),
                    CsvEscape(entry.Source ?? ""),
                    CsvEscape(entry.Status ?? ""),
                    CsvEscape(entry.PolicyVersion ?? ""),
                    entry.FiltersCreated.ToString(),
                    entry.FiltersRemoved.ToString(),
                    CsvEscape(entry.ErrorCode ?? ""),
                    CsvEscape(entry.ErrorMessage ?? ""));
                lines.Add(line);
            }

            await System.IO.File.WriteAllLinesAsync(filePath, lines);
            _dialogService.ShowSuccess($"Exported {LogEntries.Count} entries to:\n{filePath}", "Export Successful");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Failed to export:\n{ex.Message}", "Export Failed");
        }
    }

    /// <summary>
    /// Clears the filter and shows recent entries.
    /// </summary>
    [RelayCommand]
    private void ClearFilter()
    {
        UseTailFilter = true;
        TailCount = 50;
        SinceMinutes = 0;
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
