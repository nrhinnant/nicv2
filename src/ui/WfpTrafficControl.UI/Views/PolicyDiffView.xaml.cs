using System.Windows;
using WfpTrafficControl.UI.ViewModels;

namespace WfpTrafficControl.UI.Views;

/// <summary>
/// Interaction logic for PolicyDiffView.xaml
/// </summary>
public partial class PolicyDiffView : Window
{
    public PolicyDiffView(PolicyDiffViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
