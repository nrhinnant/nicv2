using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WfpTrafficControl.UI.Services;
using WfpTrafficControl.UI.Views;

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
    private BlockRulesViewModel _blockRulesViewModel;

    [ObservableProperty]
    private ConnectionMonitorViewModel _connectionMonitorViewModel;

    [ObservableProperty]
    private AnalyticsDashboardViewModel _analyticsDashboardViewModel;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _themeButtonText = "Dark";

    [ObservableProperty]
    private string _themeIcon = "\uE771"; // Moon icon (Segoe MDL2 Assets) - default for dark theme

    [ObservableProperty]
    private bool _isRollbackInProgress;

    public MainViewModel(
        IServiceClient serviceClient,
        IThemeService themeService,
        DashboardViewModel dashboardViewModel,
        PolicyEditorViewModel policyEditorViewModel,
        LogsViewModel logsViewModel,
        BlockRulesViewModel blockRulesViewModel,
        ConnectionMonitorViewModel connectionMonitorViewModel,
        AnalyticsDashboardViewModel analyticsDashboardViewModel)
    {
        _serviceClient = serviceClient;
        _themeService = themeService;
        _dashboardViewModel = dashboardViewModel;
        _policyEditorViewModel = policyEditorViewModel;
        _logsViewModel = logsViewModel;
        _blockRulesViewModel = blockRulesViewModel;
        _connectionMonitorViewModel = connectionMonitorViewModel;
        _analyticsDashboardViewModel = analyticsDashboardViewModel;

        // Subscribe to dashboard updates
        _dashboardViewModel.StatusUpdated += OnDashboardStatusUpdated;

        // Subscribe to theme changes and set initial button text and icon
        _themeService.ThemeChanged += OnThemeChanged;
        UpdateThemeButtonText(_themeService.IsDarkTheme);
        UpdateThemeIcon(_themeService.IsDarkTheme);
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

    [RelayCommand(CanExecute = nameof(CanExecuteEmergencyRollback))]
    private async Task EmergencyRollbackAsync()
    {
        // Show confirmation dialog
        var result = System.Windows.MessageBox.Show(
            "This will REMOVE ALL FILTERS and disable policy enforcement.\n" +
            "Network connections will be UNRESTRICTED.\n\n" +
            "Only use this if you are locked out of the network.\n\n" +
            "Are you sure you want to proceed?",
            "Emergency Rollback - Confirmation Required",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No); // Default to No for safety

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        IsRollbackInProgress = true;
        try
        {
            var rollbackResult = await _serviceClient.RollbackAsync();

            if (rollbackResult.IsSuccess)
            {
                // Optimistic UI update (don't wait for status refresh)
                FilterCount = 0;
                StatusMessage = $"Emergency Rollback: Removed {rollbackResult.Value.FiltersRemoved} filter(s)";

                // Show success notification
                System.Windows.MessageBox.Show(
                    $"Emergency rollback completed successfully.\n\n" +
                    $"Removed {rollbackResult.Value.FiltersRemoved} filter(s).\n" +
                    $"Network connectivity restored.",
                    "Emergency Rollback Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                // Refresh status in background
                _ = Task.Run(async () => await DashboardViewModel.RefreshStatusAsync());
            }
            else
            {
                // Check if service is offline (CRITICAL BLOCKER #1 mitigation)
                if (rollbackResult.Error.Message.Contains("Cannot connect") ||
                    rollbackResult.Error.Message.Contains("Service") ||
                    rollbackResult.Error.Message.Contains("unavailable"))
                {
                    var serviceStartResult = System.Windows.MessageBox.Show(
                        "The WfpTrafficControl service is offline.\n\n" +
                        "Would you like to start the service now?\n\n" +
                        "Note: This requires administrator privileges.",
                        "Service Offline",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);

                    if (serviceStartResult == System.Windows.MessageBoxResult.Yes)
                    {
                        try
                        {
                            StartWfpService();
                            // Retry rollback after service start
                            await EmergencyRollbackAsync();
                            return;
                        }
                        catch (Exception serviceEx)
                        {
                            System.Windows.MessageBox.Show(
                                $"Failed to start service:\n\n{serviceEx.Message}\n\n" +
                                "Please start the service manually:\n" +
                                "1. Open Services (services.msc)\n" +
                                "2. Find 'WfpTrafficControl Service'\n" +
                                "3. Click 'Start'\n\n" +
                                "Or reboot to restore connectivity.",
                                "Service Start Failed",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Error);
                            return;
                        }
                    }
                }
                else
                {
                    // Rollback failed for other reason
                    System.Windows.MessageBox.Show(
                        $"Emergency rollback failed:\n\n{rollbackResult.Error.Message}\n\n" +
                        "Filters remain active (no partial state).\n\n" +
                        "Troubleshooting:\n" +
                        "1. Verify service is running (services.msc)\n" +
                        "2. Check audit logs\n" +
                        "3. Retry rollback\n" +
                        "4. Reboot if issue persists",
                        "Emergency Rollback Failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Unexpected error during emergency rollback:\n\n{ex.Message}\n\n" +
                "Please check service status and try again.",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsRollbackInProgress = false;
        }
    }

    private bool CanExecuteEmergencyRollback() => !IsRollbackInProgress;

    private void StartWfpService()
    {
        using var serviceController = new System.ServiceProcess.ServiceController("WfpTrafficControl");

        if (serviceController.Status == System.ServiceProcess.ServiceControllerStatus.Running)
        {
            return; // Already running
        }

        serviceController.Start();
        serviceController.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
    }

    [RelayCommand]
    private void OpenRuleSimulator()
    {
        var viewModel = App.Services.GetRequiredService<RuleSimulatorViewModel>();
        var dialog = new RuleSimulatorView(viewModel);
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenPolicyDiff()
    {
        var viewModel = App.Services.GetRequiredService<PolicyDiffViewModel>();
        var dialog = new PolicyDiffView(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenPolicyHistory()
    {
        var viewModel = App.Services.GetRequiredService<PolicyHistoryViewModel>();
        var dialog = new PolicyHistoryView(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenSyslogSettings()
    {
        var viewModel = App.Services.GetRequiredService<SyslogSettingsViewModel>();
        var dialog = new SyslogSettingsView(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenNetworkProfiles()
    {
        var viewModel = App.Services.GetRequiredService<NetworkProfilesViewModel>();
        var dialog = new NetworkProfilesView(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenApplicationDiscovery()
    {
        var viewModel = App.Services.GetRequiredService<ApplicationDiscoveryViewModel>();
        var dialog = new ApplicationDiscoveryView(viewModel)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        UpdateThemeButtonText(e.IsDarkTheme);
        UpdateThemeIcon(e.IsDarkTheme);
    }

    private void UpdateThemeButtonText(bool isDarkTheme)
    {
        // Show the opposite theme as button text (what clicking will switch to)
        ThemeButtonText = isDarkTheme ? "Light" : "Dark";
    }

    private void UpdateThemeIcon(bool isDarkTheme)
    {
        // Show sun icon in dark theme (clicking switches to light)
        // Show moon icon in light theme (clicking switches to dark)
        ThemeIcon = isDarkTheme ? "\uE706" : "\uE771"; // Sun / Moon
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
