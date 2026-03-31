using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GichanDiary.Models;

namespace GichanDiary.Converters;

public class CategoryToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not EventCategory category)
            return FindBrush("CatEtcBrush");

        return category switch
        {
            EventCategory.수유 => FindBrush("CatFeedBrush"),
            EventCategory.배변 => FindBrush("CatBowelBrush"),
            EventCategory.위생관리 => FindBrush("CatHygieneBrush"),
            EventCategory.신체측정 => FindBrush("CatBodyBrush"),
            EventCategory.건강관리 => FindBrush("CatHealthBrush"),
            EventCategory.기타 => FindBrush("CatEtcBrush"),
            _ => FindBrush("CatEtcBrush"),
        };
    }

    private static Brush FindBrush(string key)
    {
        if (Application.Current?.TryFindResource(key) is Brush brush)
            return brush;
        return new SolidColorBrush(Color.FromRgb(0x50, 0x60, 0x68));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
