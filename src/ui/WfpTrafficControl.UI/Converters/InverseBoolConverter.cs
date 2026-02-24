using System.Globalization;
using System.Windows.Data;

namespace WfpTrafficControl.UI.Converters;

/// <summary>
/// Inverts a boolean value.
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}
