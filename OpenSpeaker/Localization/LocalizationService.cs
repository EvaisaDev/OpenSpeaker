using System.Windows;
namespace OpenSpeaker.Localization;

public static class LocalizationService
{
    private static string _currentCode = string.Empty;

    private static readonly Dictionary<string, string> LanguageFiles = new()
    {
        ["English"] = "en",
    };

    public static IEnumerable<string> AvailableLanguages => LanguageFiles.Keys;

    public static void Load(string languageName)
    {
        if (!LanguageFiles.TryGetValue(languageName, out var code))
            code = "en";
        if (code == _currentCode) return;

        var source = new Uri(
            $"pack://application:,,,/OpenSpeaker;component/Localization/{code}.xaml",
            UriKind.Absolute);

        var dicts = Application.Current.Resources.MergedDictionaries;
        var existing = dicts.FirstOrDefault(d =>
            d.Source?.OriginalString.Contains("/Localization/") == true);
        if (existing != null) dicts.Remove(existing);

        dicts.Add(new ResourceDictionary { Source = source });
        _currentCode = code;
    }

    public static string CurrentLanguageName()
        => LanguageFiles.FirstOrDefault(kv => kv.Value == _currentCode).Key ?? "English";
}
