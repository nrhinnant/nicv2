using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WfpTrafficControl.UI.Converters;

/// <summary>
/// Converts boolean or nullable object values to Visibility.
/// True/non-null = Visible, False/null = Collapsed (or Hidden if parameter is "Hidden").
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
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
        var useHidden = parameter is string s && s.Equals("Hidden", StringComparison.OrdinalIgnoreCase);

        return boolValue ? Visibility.Visible : (useHidden ? Visibility.Hidden : Visibility.Collapsed);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}
