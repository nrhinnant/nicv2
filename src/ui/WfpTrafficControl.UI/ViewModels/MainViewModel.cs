using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WfpTrafficControl.UI.Services;

namespace WfpTrafficControl.UI.ViewModels;

/// <summary>
/// ViewModel for the main window containing navigation and status bar.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IServiceClient _serviceClient;
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _serviceVersion = "Unknown";

    [ObservableProperty]
    private int _filterCount;

    [ObservableProperty]
    private string _lastOperationTime = "Never";

    [ObservableProperty]
    private string _statusMessage = "Checking service...";

    [ObservableProperty]
    private DashboardViewModel _dashboardViewModel;

    [ObservableProperty]
    private PolicyEditorViewModel _policyEditorViewModel;

    [ObservableProperty]
    private LogsViewModel _logsViewModel;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _themeButtonText = "Dark";

    public MainViewModel(
        IServiceClient serviceClient,
        IThemeService themeService,
        DashboardViewModel dashboardViewModel,
        PolicyEditorViewModel policyEditorViewModel,
        LogsViewModel logsViewModel)
    {
        _serviceClient = serviceClient;
        _themeService = themeService;
        _dashboardViewModel = dashboardViewModel;
        _policyEditorViewModel = policyEditorViewModel;
        _logsViewModel = logsViewModel;

        // Subscribe to dashboard updates
        _dashboardViewModel.StatusUpdated += OnDashboardStatusUpdated;

        // Subscribe to theme changes and set initial button text
        _themeService.ThemeChanged += OnThemeChanged;
        UpdateThemeButtonText(_themeService.IsDarkTheme);
    }

    private void OnDashboardStatusUpdated(object? sender, DashboardStatusEventArgs e)
    {
        IsConnected = e.IsConnected;
        ServiceVersion = e.ServiceVersion;
        FilterCount = e.FilterCount;

        if (e.LastOperationTime.HasValue)
        {
            var elapsed = DateTime.UtcNow - e.LastOperationTime.Value;
            LastOperationTime = FormatTimeAgo(elapsed);
        }

        StatusMessage = e.IsConnected ? "Connected" : "Service Offline";
    }

    private static string FormatTimeAgo(TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds < 60) return "Just now";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
        return $"{(int)elapsed.TotalDays}d ago";
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        await DashboardViewModel.RefreshStatusAsync();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
    }

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        UpdateThemeButtonText(e.IsDarkTheme);
    }

    private void UpdateThemeButtonText(bool isDarkTheme)
    {
        // Show the opposite theme as button text (what clicking will switch to)
        ThemeButtonText = isDarkTheme ? "Light" : "Dark";
    }
}

/// <summary>
/// Event args for dashboard status updates.
/// </summary>
public class DashboardStatusEventArgs : EventArgs
{
    public bool IsConnected { get; init; }
    public string ServiceVersion { get; init; } = "Unknown";
    public int FilterCount { get; init; }
    public DateTime? LastOperationTime { get; init; }
}
