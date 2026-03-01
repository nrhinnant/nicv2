using System.Windows.Controls;
using WfpTrafficControl.UI.ViewModels;

namespace WfpTrafficControl.UI.Views;

/// <summary>
/// Interaction logic for AnalyticsDashboardView.xaml
/// </summary>
public partial class AnalyticsDashboardView : UserControl
{
    public AnalyticsDashboardView()
    {
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AnalyticsDashboardViewModel vm)
        {
            vm.StartCollection();
        }
    }

    private void UserControl_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AnalyticsDashboardViewModel vm)
        {
            vm.StopCollection();
        }
    }
}
