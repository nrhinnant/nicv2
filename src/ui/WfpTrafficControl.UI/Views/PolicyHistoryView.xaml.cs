using System.Windows;
using WfpTrafficControl.UI.ViewModels;

namespace WfpTrafficControl.UI.Views;

/// <summary>
/// Interaction logic for PolicyHistoryView.xaml
/// </summary>
public partial class PolicyHistoryView : Window
{
    public PolicyHistoryView(PolicyHistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Load history when window opens
        Loaded += async (_, _) => await viewModel.RefreshHistoryCommand.ExecuteAsync(null);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
