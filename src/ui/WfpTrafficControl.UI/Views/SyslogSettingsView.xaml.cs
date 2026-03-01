using System.Windows;
using WfpTrafficControl.UI.ViewModels;

namespace WfpTrafficControl.UI.Views;

/// <summary>
/// Interaction logic for SyslogSettingsView.xaml
/// </summary>
public partial class SyslogSettingsView : Window
{
    public SyslogSettingsView(SyslogSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SyslogSettingsViewModel vm)
        {
            await vm.LoadConfigAsync();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
