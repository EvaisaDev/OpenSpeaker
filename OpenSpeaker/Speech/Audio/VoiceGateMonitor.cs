using NAudio.Wave;
namespace OpenSpeaker.Audio;

public class VoiceGateMonitor : IDisposable
{
    private WaveInEvent? _waveIn;
    private float _thresholdDb = -30f;
    private bool _isActive = false;
    private int _deviceNumber = 0;

    public event EventHandler? ThresholdExceeded;
    public event EventHandler? ThresholdCleared;

    public bool IsAboveThreshold { get; private set; } = false;

    public void Start(int deviceNumber, float thresholdDb)
    {
        Stop();
        _deviceNumber = deviceNumber;
        _thresholdDb = thresholdDb;
        _isActive = true;

        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
    }

    public void Stop()
    {
        _isActive = false;
        if (_waveIn != null)
        {
            _waveIn.StopRecording();
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.Dispose();
            _waveIn = null;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!_isActive) return;

        double sum = 0;
        int count = e.BytesRecorded / 2;
        for (int i = 0; i < e.BytesRecorded; i += 2)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i);
            double normalized = sample / 32768.0;
            sum += normalized * normalized;
        }

        double rms = count > 0 ? Math.Sqrt(sum / count) : 0;
        double db = rms > 0 ? 20 * Math.Log10(rms) : -100;
        bool above = db >= _thresholdDb;

        if (above && !IsAboveThreshold)
        {
            IsAboveThreshold = true;
            ThresholdExceeded?.Invoke(this, EventArgs.Empty);
        }
        else if (!above && IsAboveThreshold)
        {
            IsAboveThreshold = false;
            ThresholdCleared?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => Stop();
}
