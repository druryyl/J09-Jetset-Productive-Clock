using System.Windows;

namespace Jetset.App;

public static class ThemeManager
{
    public static void Apply(bool dark)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        var dict = new ResourceDictionary
        {
            Source = new Uri(dark
                ? "Themes/DarkTheme.xaml"
                : "Themes/LightTheme.xaml", UriKind.Relative)
        };

        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(dict);
    }
}
