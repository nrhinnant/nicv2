using System.Windows;
using WfpTrafficControl.UI.ViewModels;

namespace WfpTrafficControl.UI.Views;

/// <summary>
/// Interaction logic for ApplicationDiscoveryView.xaml
/// </summary>
public partial class ApplicationDiscoveryView : Window
{
    public ApplicationDiscoveryView(ApplicationDiscoveryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Auto-scan on load
        if (DataContext is ApplicationDiscoveryViewModel vm)
        {
            await vm.ScanCommand.ExecuteAsync(null);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
