using System.Windows;
using System.Windows.Controls;

namespace WfpTrafficControl.UI.Controls;

/// <summary>
/// A reusable search filter control with a search text box and clear button.
/// </summary>
public partial class SearchFilterControl : UserControl
{
    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(
            nameof(SearchText),
            typeof(string),
            typeof(SearchFilterControl),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder),
            typeof(string),
            typeof(SearchFilterControl),
            new PropertyMetadata("Search..."));

    public SearchFilterControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the placeholder text shown when search is empty.
    /// </summary>
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        SearchText = string.Empty;
        SearchTextBox.Focus();
    }
}
