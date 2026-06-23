using System.Windows;
namespace OpenSpeaker.Themes;

public static class ThemeService
{
    private static string _current = string.Empty;

    public static IEnumerable<string> AvailableThemes { get; } = new[] { "Dark", "Light" };

    public static void Apply(string theme)
    {
        if (theme == _current) return;
        if (!AvailableThemes.Contains(theme)) theme = "Dark";

        var source = new Uri(
            $"pack://application:,,,/OpenSpeaker;component/Themes/{theme}Theme.xaml",
            UriKind.Absolute);

        var dicts = Application.Current.Resources.MergedDictionaries;
        var existing = dicts.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("/Themes/") == true);
        if (existing != null) dicts.Remove(existing);

        dicts.Add(new ResourceDictionary { Source = source });
        _current = theme;
    }
}
