using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GichanDiary.Converters;

/// <summary>
/// Returns Visible when the bound enum value equals the converter parameter; Collapsed otherwise.
/// Usage: Visibility="{Binding SelectedCategory, Converter={StaticResource EnumEqualToVisibility}, ConverterParameter=수유}"
/// </summary>
public class EnumEqualToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        var enumStr = parameter.ToString();
        var valueStr = value.ToString();

        return string.Equals(enumStr, valueStr, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
