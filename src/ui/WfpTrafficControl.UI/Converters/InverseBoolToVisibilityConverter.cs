using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WfpTrafficControl.UI.Converters;

/// <summary>
/// Converts boolean or nullable object values to Visibility (inverted).
/// True/non-null = Collapsed, False/null = Visible.
/// </summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Handle both bool and nullable object (null = false, non-null = true)
        var boolValue = value switch
        {
            bool b => b,
            null => false,
            _ => true  // Non-null object is treated as true
        };
        return boolValue ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v != Visibility.Visible;
    }
}
