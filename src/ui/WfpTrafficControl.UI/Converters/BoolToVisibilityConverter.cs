using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WfpTrafficControl.UI.Converters;

/// <summary>
/// Converts boolean values to Visibility.
/// True = Visible, False = Collapsed (or Hidden if parameter is "Hidden").
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var boolValue = value is bool b && b;
        var useHidden = parameter is string s && s.Equals("Hidden", StringComparison.OrdinalIgnoreCase);

        return boolValue ? Visibility.Visible : (useHidden ? Visibility.Hidden : Visibility.Collapsed);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}
