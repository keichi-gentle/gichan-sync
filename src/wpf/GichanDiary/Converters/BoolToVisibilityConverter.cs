using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GichanDiary.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var b = value is bool bVal && bVal;

        // If parameter is "Inverse", invert the boolean
        if (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            b = !b;

        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var result = value is Visibility v && v == Visibility.Visible;

        if (parameter is string s && s.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            result = !result;

        return result;
    }
}
