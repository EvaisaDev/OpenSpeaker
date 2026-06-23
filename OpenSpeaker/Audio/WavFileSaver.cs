using System.IO;
using NAudio.Wave;
using OpenSpeaker.TTS;
namespace OpenSpeaker.Audio;

public class WavFileSaver
{
    public string Save(AudioData audio, string folder, IReadOnlyList<string> paramValues, string? aliasName, string? username)
    {
        if (audio.IsEmpty) return string.Empty;
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        var ts         = DateTime.Now.ToString("yyyyMMddTHHmmss");
        var id         = "_" + GenerateId();
        var paramPart  = paramValues.Count > 0 ? string.Join("-", paramValues.Select(Sanitize)) + "-" : string.Empty;
        var aliasPart  = string.IsNullOrEmpty(aliasName) ? string.Empty : Sanitize(aliasName) + "-";
        var userPart   = string.IsNullOrEmpty(username)  ? string.Empty : Sanitize(username);
        var suffix     = (paramPart + aliasPart + userPart).TrimEnd('-');
        var fileName   = $"{ts}{id}-{suffix}.wav";
        var path       = Path.Combine(folder, fileName);

        using var ms     = new MemoryStream(audio.Samples);
        using var reader = new RawSourceWaveStream(ms, audio.Format);
        WaveFileWriter.CreateWaveFile(path, reader);

        return path;
    }

    private static string GenerateId()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Range(0, 8).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }

    private static string Sanitize(string s) =>
        string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
