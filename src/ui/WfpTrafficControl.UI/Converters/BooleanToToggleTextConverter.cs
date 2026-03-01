using System.Globalization;
using System.Windows.Data;

namespace WfpTrafficControl.UI.Converters;

/// <summary>
/// Converts a boolean to one of two strings based on the value.
/// Parameter format: "TrueText|FalseText"
/// </summary>
public sealed class BooleanToToggleTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isTrue = value is bool b && b;
        var texts = (parameter as string)?.Split('|') ?? new[] { "On", "Off" };

        if (texts.Length < 2)
        {
            return isTrue ? "On" : "Off";
        }

        return isTrue ? texts[0] : texts[1];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
