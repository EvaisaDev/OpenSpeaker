using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Infrastructure.Http;
namespace OpenSpeaker.Services;

public static class UpdateService
{
    private const string Repo = "EvaisaDev/OpenSpeaker";
    private const string GitHubClientName = "github";

    public record UpdateInfo(
        bool IsAvailable,
        string CurrentVersion,
        string LatestVersion,
        string? DownloadUrl,
        string? AssetName,
        string ReleaseUrl);

    public static string CurrentVersion
    {
        get
        {
            var info = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(info))
                info = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
            var plus = info.IndexOf('+');
            if (plus >= 0) info = info.Substring(0, plus);
            return info;
        }
    }

    public static async Task<UpdateInfo> CheckAsync()
    {
        var current = CurrentVersion;
        try
        {
            var client = HttpClientFactory.GetClient(GitHubClientName);
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{Repo}/releases/latest");
            req.Headers.Add("Accept", "application/vnd.github+json");
            using var resp = await client.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return new UpdateInfo(false, current, current, null, null, "");

            var json = await resp.Content.ReadAsStringAsync();
            var release = JObject.Parse(json);
            var tag = (string?)release["tag_name"] ?? "";
            var latest = tag.TrimStart('v', 'V');
            var releaseUrl = (string?)release["html_url"] ?? "";

            var keyword = IsStandaloneInstall() ? "standalone" : "portable";
            string? url = null, assetName = null;
            if (release["assets"] is JArray assets)
            {
                foreach (var a in assets)
                {
                    var name = (string?)a["name"] ?? "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                        name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    {
                        url = (string?)a["browser_download_url"];
                        assetName = name;
                        break;
                    }
                }
                if (url == null)
                {
                    foreach (var a in assets)
                    {
                        var name = (string?)a["name"] ?? "";
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            url = (string?)a["browser_download_url"];
                            assetName = name;
                            break;
                        }
                    }
                }
            }

            var available = url != null && IsNewer(latest, current);
            return new UpdateInfo(available, current, latest, url, assetName, releaseUrl);
        }
        catch
        {
            return new UpdateInfo(false, current, current, null, null, "");
        }
    }

    public static async Task<string?> GetReleaseNotesAsync(string version)
    {
        try
        {
            var client = HttpClientFactory.GetClient(GitHubClientName);
            foreach (var tag in new[] { version, "v" + version })
            {
                using var req = new HttpRequestMessage(HttpMethod.Get,
                    $"https://api.github.com/repos/{Repo}/releases/tags/{tag}");
                req.Headers.Add("Accept", "application/vnd.github+json");
                using var resp = await client.SendAsync(req);
                if (!resp.IsSuccessStatusCode) continue;

                var json = await resp.Content.ReadAsStringAsync();
                var release = JObject.Parse(json);
                return ExtractChangelog((string?)release["body"]);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractChangelog(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var idx = body.IndexOf("## Changelog", StringComparison.OrdinalIgnoreCase);
        return (idx >= 0 ? body.Substring(idx) : body).Trim();
    }

    public static async Task ApplyAsync(UpdateInfo info)
    {
        if (info.DownloadUrl == null) return;

        var root = Path.Combine(Path.GetTempPath(), "OpenSpeaker_update");
        var staging = Path.Combine(root, "staging");
        var zipPath = Path.Combine(root, "update.zip");
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.CreateDirectory(staging);

        var client = HttpClientFactory.GetClient(GitHubClientName);
        using (var resp = await client.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            await using var fs = File.Create(zipPath);
            await resp.Content.CopyToAsync(fs);
        }

        ZipFile.ExtractToDirectory(zipPath, staging, true);

        var scriptPath = Path.Combine(root, "apply_update.ps1");
        await File.WriteAllTextAsync(scriptPath, UpdaterScript);

        var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "OpenSpeaker.exe");
        var dest = AppContext.BaseDirectory.TrimEnd('\\', '/');

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-WindowStyle");
        psi.ArgumentList.Add("Hidden");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("-ProcessId");
        psi.ArgumentList.Add(Environment.ProcessId.ToString());
        psi.ArgumentList.Add("-Source");
        psi.ArgumentList.Add(staging);
        psi.ArgumentList.Add("-Dest");
        psi.ArgumentList.Add(dest);
        psi.ArgumentList.Add("-Exe");
        psi.ArgumentList.Add(exe);
        Process.Start(psi);

        Application.Current.Shutdown();
    }

    private static bool IsStandaloneInstall()
        => File.Exists(Path.Combine(AppContext.BaseDirectory, "wpfgfx_cor3.dll"));

    private static bool IsNewer(string latest, string current)
        => TryParseVersion(latest, out var l) && TryParseVersion(current, out var c) && l > c;

    private static bool TryParseVersion(string value, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value)) return false;
        var core = value.Split('-', '+')[0].Trim();
        if (!core.Contains('.')) core += ".0";
        return Version.TryParse(core, out version!);
    }

    private const string UpdaterScript =
        "param([int]$ProcessId,[string]$Source,[string]$Dest,[string]$Exe)\n" +
        "try { Wait-Process -Id $ProcessId -Timeout 120 -ErrorAction SilentlyContinue } catch {}\n" +
        "Start-Sleep -Milliseconds 800\n" +
        "robocopy $Source $Dest /E /R:3 /W:1 /NFL /NDL /NJH /NJS /NC /NS | Out-Null\n" +
        "Start-Process -FilePath $Exe -WorkingDirectory $Dest\n" +
        "Remove-Item -LiteralPath $Source -Recurse -Force -ErrorAction SilentlyContinue\n";
}
