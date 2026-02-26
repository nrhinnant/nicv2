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

        var mainWindow = new MainWindow
        {
            DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
        };
        mainWindow.Show();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<IServiceClient, ServiceClient>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IPolicyTemplateProvider, PolicyTemplateProvider>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<PolicyEditorViewModel>();
    }
}
