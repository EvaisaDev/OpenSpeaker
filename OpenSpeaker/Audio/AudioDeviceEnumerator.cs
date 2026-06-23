using NAudio.Wave;
using OpenSpeaker.ThingsIDKWhereToPut.Logging;
namespace OpenSpeaker.Audio;
public class AudioDeviceEnumerator
{
    private readonly IAppLogger? _logger;

    public AudioDeviceEnumerator(IAppLogger? logger = null)
    {
        _logger = logger;
    }

    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        devices.Add(new AudioDeviceInfo { Id = string.Empty, Name = "System Default", IsDefault = true });

        _logger?.Info($"AUDIO :: WaveOut.DeviceCount = {WaveOut.DeviceCount}");
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            _logger?.Info($"AUDIO :: Output device {i}: {caps.ProductName}");
            devices.Add(new AudioDeviceInfo { Id = i.ToString(), Name = caps.ProductName, IsDefault = false });
        }

        _logger?.Info($"AUDIO :: Total output devices loaded: {devices.Count}");
        return devices;
    }

    public IReadOnlyList<AudioDeviceInfo> GetInputDevices()
    {
        var devices = new List<AudioDeviceInfo>();

        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            devices.Add(new AudioDeviceInfo { Id = i.ToString(), Name = caps.ProductName, IsDefault = i == 0 });
        }

        return devices;
    }
}
