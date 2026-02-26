using System.Globalization;
using System.Windows.Data;

namespace WfpTrafficControl.UI.Converters;

/// <summary>
/// Converts objects to boolean values.
/// Non-null objects convert to true, null converts to false.
/// </summary>
public sealed class ObjectToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("ObjectToBoolConverter does not support ConvertBack.");
    }
}
