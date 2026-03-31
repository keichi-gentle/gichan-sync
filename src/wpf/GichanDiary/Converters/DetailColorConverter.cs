using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using GichanDiary.Models;

namespace GichanDiary.Converters;

public class DetailColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not BabyEvent evt)
            return FindBrush("TextLightBrush");

        return evt.Category switch
        {
            EventCategory.수유 => FindBrush("CatFeedBrush"),
            EventCategory.배변 => GetBowelBrush(evt),
            EventCategory.위생관리 => FindBrush("CatHygieneBrush"),
            EventCategory.신체측정 => FindBrush("CatBodyBrush"),
            EventCategory.건강관리 => FindBrush("CatHealthBrush"),
            EventCategory.기타 => FindBrush("CatEtcBrush"),
            _ => FindBrush("TextLightBrush"),
        };
    }

    private static Brush GetBowelBrush(BabyEvent evt)
    {
        if (evt.HasUrine == true && evt.HasStool != true)
            return FindBrush("CatUrineBrush");
        if (evt.HasStool == true && evt.HasUrine != true)
            return FindBrush("CatStoolBrush");
        return FindBrush("CatBowelBrush");
    }

    private static Brush FindBrush(string key)
    {
        if (Application.Current?.TryFindResource(key) is Brush brush)
            return brush;
        return new SolidColorBrush(Color.FromRgb(0xC8, 0xD0, 0xDC));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
