using System.Windows;
using WfpTrafficControl.UI.ViewModels;

namespace WfpTrafficControl.UI;

/// <summary>
/// Main application window.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize the dashboard when the window loads
        if (DataContext is MainViewModel mainVm)
        {
            await mainVm.DashboardViewModel.InitializeAsync();
        }
    }
}
