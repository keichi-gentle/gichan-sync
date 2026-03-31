using System.Globalization;
using System.Windows.Data;

namespace GichanDiary.Converters;

public class TimeSpanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TimeSpan ts)
            return string.Empty;

        int totalHours = (int)ts.TotalHours;
        int minutes = ts.Minutes;

        return $"{totalHours}시간 {minutes}분";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
