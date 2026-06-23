using System.IO;
using System.Text.Json;
using OpenSpeaker.Models;
namespace OpenSpeaker.Services;

public class ProfileService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _dataDir;
    private readonly string _manifestPath;

    public ProfileService(string dataDir)
    {
        _dataDir = dataDir;
        _manifestPath = Path.Combine(dataDir, "profiles.json");
    }

    public ProfileManifest Load()
    {
        if (!File.Exists(_manifestPath)) return new ProfileManifest();
        try { return JsonSerializer.Deserialize<ProfileManifest>(File.ReadAllText(_manifestPath)) ?? new ProfileManifest(); }
        catch
        {
            TryBackupCorruptManifest();
            return new ProfileManifest();
        }
    }

    private void TryBackupCorruptManifest()
    {
        try
        {
            if (!File.Exists(_manifestPath)) return;
            var backup = _manifestPath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            File.Copy(_manifestPath, backup, overwrite: true);
        }
        catch { }
    }

    public void Save(ProfileManifest manifest) =>
        File.WriteAllText(_manifestPath, JsonSerializer.Serialize(manifest, JsonOpts));

    public string GetDbPath(string profileName)
    {
        if (profileName == "Default") return Path.Combine(_dataDir, "openspeaker.db");
        return Path.Combine(_dataDir, "profile_" + Sanitize(profileName) + ".db");
    }

    public void SetActive(string name)
    {
        var m = Load(); m.ActiveProfile = name; Save(m);
    }

    public bool AddProfile(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var m = Load();
        if (m.Profiles.Contains(name)) return false;
        m.Profiles.Add(name);
        Save(m);
        return true;
    }

    private static string Sanitize(string name) =>
        new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
}
