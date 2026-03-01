using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WfpTrafficControl.UI.Services;
using WfpTrafficControl.UI.ViewModels;

namespace WfpTrafficControl.UI;

/// <summary>
/// Application entry point with dependency injection configuration.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private MainWindow? _mainWindow;
    private ITrayIconService? _trayIconService;
    private ThemeService? _themeService;
    private MainViewModel? _mainViewModel;

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        // Initialize theme service (must be done before showing any UI)
        _themeService = _serviceProvider.GetRequiredService<IThemeService>() as ThemeService;
        _themeService?.Initialize();

        // Initialize tray icon service
        _trayIconService = _serviceProvider.GetRequiredService<ITrayIconService>();
        _trayIconService.Initialize();
        _trayIconService.ShowWindowRequested += OnTrayShowWindowRequested;
        _trayIconService.ExitRequested += OnTrayExitRequested;
        _trayIconService.RefreshRequested += OnTrayRefreshRequested;

        // Create main window and view model
        _mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

        // Subscribe to status updates to update tray icon
        _mainViewModel.DashboardViewModel.StatusUpdated += OnDashboardStatusUpdated;

        _mainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };

        // Wire up minimize-to-tray behavior
        _mainWindow.StateChanged += OnMainWindowStateChanged;
        _mainWindow.Closing += OnMainWindowClosing;

        _mainWindow.Show();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        // Clean up theme service
        _themeService?.Cleanup();

        // Dispose tray icon
        if (_trayIconService != null)
        {
            _trayIconService.ShowWindowRequested -= OnTrayShowWindowRequested;
            _trayIconService.ExitRequested -= OnTrayExitRequested;
            _trayIconService.RefreshRequested -= OnTrayRefreshRequested;
            _trayIconService.Dispose();
        }

        _serviceProvider?.Dispose();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<IServiceClient, ServiceClient>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IPolicyTemplateProvider, PolicyTemplateProvider>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<IThemeService, ThemeService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PolicyEditorViewModel>();
        services.AddTransient<LogsViewModel>();
        services.AddTransient<BlockRulesViewModel>();
        services.AddTransient<RuleSimulatorViewModel>();
        services.AddTransient<PolicyDiffViewModel>();
    }

    private void OnDashboardStatusUpdated(object? sender, DashboardStatusEventArgs e)
    {
        _trayIconService?.UpdateState(e.IsConnected, e.FilterCount, e.ServiceVersion);
    }

    private void OnTrayShowWindowRequested(object? sender, EventArgs e)
    {
        if (_mainWindow == null)
            return;

        // Show and restore window
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OnTrayExitRequested(object? sender, EventArgs e)
    {
        // Force close the application
        _mainWindow?.Close();
        Shutdown();
    }

    private async void OnTrayRefreshRequested(object? sender, EventArgs e)
    {
        if (_mainViewModel != null)
        {
            await _mainViewModel.RefreshStatusCommand.ExecuteAsync(null);
        }
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow?.WindowState == WindowState.Minimized)
        {
            // Hide to tray when minimized
            _mainWindow.Hide();
            _trayIconService?.ShowNotification(
                "WFP Traffic Control",
                "Application minimized to system tray",
                isError: false);
        }
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // When closing, minimize to tray instead of exiting (unless explicitly exiting)
        if (_mainWindow != null && _mainWindow.WindowState != WindowState.Minimized)
        {
            e.Cancel = true;
            _mainWindow.WindowState = WindowState.Minimized;
        }
    }
}
