using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GichanDiary.Converters;

/// <summary>
/// Returns a highlighted border brush when the bound enum value equals the converter parameter.
/// Used for category button highlighting.
/// </summary>
public class EnumEqualToBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // values[0] = SelectedCategory (enum), values[1] = this button's category (string param from Tag)
        if (values.Length < 2 || values[0] == null || values[1] == null)
            return new SolidColorBrush(Color.FromRgb(0x2A, 0x30, 0x50)); // BorderColor

        var selected = values[0].ToString();
        var buttonCat = values[1].ToString();

        if (string.Equals(selected, buttonCat, StringComparison.Ordinal))
        {
            // Return category-specific highlight brush
            return buttonCat switch
            {
                "수유" => new SolidColorBrush(Color.FromRgb(0x1E, 0x80, 0x70)),
                "배변" => new SolidColorBrush(Color.FromRgb(0xB8, 0x80, 0x20)),
                "위생관리" => new SolidColorBrush(Color.FromRgb(0x30, 0x58, 0x98)),
                "신체측정" => new SolidColorBrush(Color.FromRgb(0x70, 0x48, 0x90)),
                "건강관리" => new SolidColorBrush(Color.FromRgb(0xA0, 0x40, 0x40)),
                "기타" => new SolidColorBrush(Color.FromRgb(0x50, 0x60, 0x68)),
                _ => new SolidColorBrush(Color.FromRgb(0x2A, 0x30, 0x50)),
            };
        }

        return new SolidColorBrush(Color.FromRgb(0x2A, 0x30, 0x50)); // BorderColor
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
