using System.IO;
using System.Windows;
namespace OpenSpeaker.Themes;

public static class ThemeService
{
    private static string _current = string.Empty;

    private static readonly string[] BuiltInThemes = { "Dark", "Light" };

    public static string ThemesDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes");

    public static IEnumerable<string> AvailableThemes
    {
        get
        {
            var names = new List<string>(BuiltInThemes);
            if (Directory.Exists(ThemesDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(ThemesDirectory, "*.xaml"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                        names.Add(name);
                }
            }
            return names;
        }
    }

    public static void Apply(string theme)
    {
        if (theme == _current) return;

        var dict = TryLoadExternal(theme) ?? TryLoadBuiltIn(theme);
        if (dict == null)
        {
            theme = "Dark";
            dict = TryLoadExternal(theme) ?? TryLoadBuiltIn(theme);
        }
        if (dict == null) return;

        var dicts = Application.Current.Resources.MergedDictionaries;
        var existing = dicts.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("/Themes/") == true);
        if (existing != null) dicts.Remove(existing);

        dicts.Add(dict);
        _current = theme;
    }

    private static ResourceDictionary? TryLoadExternal(string theme)
    {
        var path = Path.Combine(ThemesDirectory, theme + ".xaml");
        if (!File.Exists(path)) return null;
        try { return new ResourceDictionary { Source = new Uri(path, UriKind.Absolute) }; }
        catch { return null; }
    }

    private static ResourceDictionary? TryLoadBuiltIn(string theme)
    {
        if (!BuiltInThemes.Any(t => string.Equals(t, theme, StringComparison.OrdinalIgnoreCase)))
            return null;
        try
        {
            var source = new Uri(
                $"pack://application:,,,/OpenSpeaker;component/Themes/{theme}Theme.xaml",
                UriKind.Absolute);
            return new ResourceDictionary { Source = source };
        }
        catch { return null; }
    }
}
