using System.IO;
using System.Windows;
namespace OpenSpeaker.Localization;

public static class LocalizationService
{
    private static string _current = string.Empty;

    private static readonly Dictionary<string, string> BuiltInLanguages = new()
    {
        ["English"] = "en",
    };

    public static string LocalizationDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Localization");

    public static IEnumerable<string> AvailableLanguages
    {
        get
        {
            var names = new List<string>(BuiltInLanguages.Keys);
            if (Directory.Exists(LocalizationDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(LocalizationDirectory, "*.xaml"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                        names.Add(name);
                }
            }
            return names;
        }
    }

    public static void Load(string languageName)
    {
        if (languageName == _current) return;

        var dict = TryLoadExternal(languageName) ?? TryLoadBuiltIn(languageName);
        if (dict == null)
        {
            languageName = "English";
            dict = TryLoadExternal(languageName) ?? TryLoadBuiltIn(languageName);
        }
        if (dict == null) return;

        var dicts = Application.Current.Resources.MergedDictionaries;
        var existing = dicts.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("/Localization/") == true);
        if (existing != null) dicts.Remove(existing);

        dicts.Add(dict);
        _current = languageName;
    }

    public static string CurrentLanguageName() => _current.Length == 0 ? "English" : _current;

    private static ResourceDictionary? TryLoadExternal(string name)
    {
        var path = Path.Combine(LocalizationDirectory, name + ".xaml");
        if (!File.Exists(path)) return null;
        try { return new ResourceDictionary { Source = new Uri(path, UriKind.Absolute) }; }
        catch { return null; }
    }

    private static ResourceDictionary? TryLoadBuiltIn(string name)
    {
        if (!BuiltInLanguages.TryGetValue(name, out var code)) return null;
        try
        {
            var source = new Uri(
                $"pack://application:,,,/OpenSpeaker;component/Localization/{code}.xaml",
                UriKind.Absolute);
            return new ResourceDictionary { Source = source };
        }
        catch { return null; }
    }
}
