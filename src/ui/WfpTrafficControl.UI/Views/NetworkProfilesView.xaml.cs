using System.Windows;
using WfpTrafficControl.UI.ViewModels;

namespace WfpTrafficControl.UI.Views;

/// <summary>
/// Interaction logic for NetworkProfilesView.xaml
/// </summary>
public partial class NetworkProfilesView : Window
{
    public NetworkProfilesView(NetworkProfilesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is NetworkProfilesViewModel vm)
        {
            await vm.LoadAsync();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
