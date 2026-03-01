using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WfpTrafficControl.UI.Converters;

/// <summary>
/// Converts null values to Visibility.
/// Non-null = Visible, null = Collapsed (or Hidden if parameter is "Hidden").
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasValue = value != null && (value is not string s || !string.IsNullOrEmpty(s));
        var useHidden = parameter is string p && p.Equals("Hidden", StringComparison.OrdinalIgnoreCase);

        return hasValue ? Visibility.Visible : (useHidden ? Visibility.Hidden : Visibility.Collapsed);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverse of NullToVisibilityConverter.
/// Null = Visible, non-null = Collapsed.
/// </summary>
public sealed class InverseNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var hasValue = value != null && (value is not string s || !string.IsNullOrEmpty(s));
        var useHidden = parameter is string p && p.Equals("Hidden", StringComparison.OrdinalIgnoreCase);

        return hasValue ? (useHidden ? Visibility.Hidden : Visibility.Collapsed) : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
