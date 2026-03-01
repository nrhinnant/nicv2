using System.Windows;
using WfpTrafficControl.UI.ViewModels;

namespace WfpTrafficControl.UI.Views;

/// <summary>
/// Interaction logic for RuleSimulatorView.xaml
/// </summary>
public partial class RuleSimulatorView : Window
{
    public RuleSimulatorView(RuleSimulatorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
