using OpenSpeaker.Audio;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
namespace OpenSpeaker.Services;

public class VoiceGateService : IDisposable
{
    private readonly VoiceGateMonitor _monitor;
    private readonly ITtsQueue _queue;
    private readonly DatabaseContext _db;
    private System.Threading.Timer? _resumeTimer;
    private bool _pausedByGate = false;
    private int _timeoutMs = 1000;

    public bool IsActive { get; private set; } = false;

    public VoiceGateService(VoiceGateMonitor monitor, ITtsQueue queue, DatabaseContext db)
    {
        _monitor = monitor;
        _queue = queue;
        _db = db;

        _monitor.ThresholdExceeded += OnThresholdExceeded;
        _monitor.ThresholdCleared += OnThresholdCleared;
    }

    public void ActivateProfile(string profileName)
    {
        VoiceGateProfile? profile = null;

        if (profileName.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            Deactivate();
            return;
        }

        profile = _db.VoiceGateProfiles.FindOne(p => p.Name == profileName);
        if (profile == null) return;

        Activate(profile);
    }

    public void Activate(VoiceGateProfile profile)
    {
        _timeoutMs = profile.TimeoutMs;
        int.TryParse(profile.DeviceId, out var deviceNumber);
        _monitor.Start(deviceNumber, profile.ThresholdDb);
        IsActive = true;
    }

    public void Deactivate()
    {
        _monitor.Stop();
        IsActive = false;
        if (_pausedByGate)
        {
            _queue.Resume();
            _pausedByGate = false;
        }
    }

    private void OnThresholdExceeded(object? sender, EventArgs e)
    {
        _resumeTimer?.Dispose();
        _resumeTimer = null;

        if (!_pausedByGate)
        {
            _pausedByGate = true;
            _queue.Pause();
        }
    }

    private void OnThresholdCleared(object? sender, EventArgs e)
    {
        _resumeTimer?.Dispose();
        _resumeTimer = new System.Threading.Timer(_ =>
        {
            if (_pausedByGate)
            {
                _pausedByGate = false;
                _queue.Resume();
            }
        }, null, _timeoutMs, System.Threading.Timeout.Infinite);
    }

    public void Dispose()
    {
        _resumeTimer?.Dispose();
        _monitor.Dispose();
    }
}
