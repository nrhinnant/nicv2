using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace WfpTrafficControl.UI.Views;

/// <summary>
/// Dialog for selecting a process from the list of running processes.
/// </summary>
public partial class ProcessPickerDialog : Window, INotifyPropertyChanged
{
    private string _searchText = "";
    private ProcessInfo? _selectedProcess;
    private bool _isLoading;
    private readonly ObservableCollection<ProcessInfo> _allProcesses = new();
    private readonly ObservableCollection<ProcessInfo> _filteredProcesses = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets the search text for filtering processes.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            FilterProcesses();
        }
    }

    /// <summary>
    /// Gets or sets the selected process.
    /// </summary>
    public ProcessInfo? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            _selectedProcess = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    /// <summary>
    /// Gets whether a process is selected.
    /// </summary>
    public bool HasSelection => SelectedProcess != null && !string.IsNullOrEmpty(SelectedProcess.ProcessPath);

    /// <summary>
    /// Gets the filtered list of processes.
    /// </summary>
    public ObservableCollection<ProcessInfo> FilteredProcesses => _filteredProcesses;

    /// <summary>
    /// Gets whether the dialog is loading processes.
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets whether to show the empty state.
    /// </summary>
    public bool ShowEmptyState => !IsLoading && FilteredProcesses.Count == 0 && !string.IsNullOrEmpty(SearchText);

    /// <summary>
    /// Gets the selected process path, or null if cancelled.
    /// </summary>
    public string? SelectedPath { get; private set; }

    public ProcessPickerDialog()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadProcessesAsync();
        SearchTextBox.Focus();
    }

    private async Task LoadProcessesAsync()
    {
        IsLoading = true;
        _allProcesses.Clear();
        _filteredProcesses.Clear();

        try
        {
            // Load processes on background thread
            var processes = await Task.Run(() =>
            {
                var list = new List<ProcessInfo>();
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        var info = new ProcessInfo
                        {
                            ProcessId = process.Id,
                            ProcessName = process.ProcessName
                        };

                        // Try to get the full path
                        try
                        {
                            info.ProcessPath = process.MainModule?.FileName ?? "";
                        }
                        catch
                        {
                            // Access denied for some system processes
                            info.ProcessPath = "";
                        }

                        // Only include processes with valid paths
                        if (!string.IsNullOrEmpty(info.ProcessPath))
                        {
                            list.Add(info);
                        }
                    }
                    catch
                    {
                        // Skip processes we can't access
                    }
                }

                // Sort by process name
                return list.OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase).ToList();
            });

            // Update on UI thread
            foreach (var process in processes)
            {
                _allProcesses.Add(process);
            }

            FilterProcesses();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading processes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }

    private void FilterProcesses()
    {
        _filteredProcesses.Clear();

        var search = SearchText?.Trim() ?? "";

        foreach (var process in _allProcesses)
        {
            if (string.IsNullOrEmpty(search) ||
                process.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                process.ProcessPath.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                _filteredProcesses.Add(process);
            }
        }

        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        // Already handled by binding, but keep for any additional logic
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadProcessesAsync();
    }

    private void ProcessGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HasSelection)
        {
            SelectAndClose();
        }
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        SelectAndClose();
    }

    private void SelectAndClose()
    {
        if (SelectedProcess != null && !string.IsNullOrEmpty(SelectedProcess.ProcessPath))
        {
            SelectedPath = SelectedProcess.ProcessPath;
            DialogResult = true;
            Close();
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Information about a running process.
/// </summary>
public class ProcessInfo
{
    /// <summary>
    /// Gets or sets the process ID.
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Gets or sets the process name.
    /// </summary>
    public string ProcessName { get; set; } = "";

    /// <summary>
    /// Gets or sets the full path to the process executable.
    /// </summary>
    public string ProcessPath { get; set; } = "";
}
