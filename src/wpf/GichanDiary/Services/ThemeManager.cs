using System.Linq;
using System.Windows;

namespace GichanDiary.Services;

public static class ThemeManager
{
    /// <summary>
    /// 테마 적용: App.Resources.MergedDictionaries 최상위에서 Colors 교체.
    /// 최상위에서 교체해야 DynamicResource가 모든 요소에 즉시 전파됩니다.
    /// </summary>
    public static void ApplyTheme(string theme)
    {
        var app = Application.Current;
        if (app == null) return;

        var mergedDicts = app.Resources.MergedDictionaries;

        // 기존 Colors dictionary 제거 (Colors.xaml 또는 LightColors.xaml)
        var colorsDict = mergedDicts
            .FirstOrDefault(d => d.Source != null && d.Source.ToString().Contains("Colors"));
        if (colorsDict != null)
            mergedDicts.Remove(colorsDict);

        // 새 Colors dictionary를 맨 앞에 삽입 (DarkTheme.xaml보다 앞)
        var uri = theme == "Light"
            ? new System.Uri("Resources/Themes/LightColors.xaml", System.UriKind.Relative)
            : new System.Uri("Resources/Colors.xaml", System.UriKind.Relative);

        mergedDicts.Insert(0, new ResourceDictionary { Source = uri });
    }
}
