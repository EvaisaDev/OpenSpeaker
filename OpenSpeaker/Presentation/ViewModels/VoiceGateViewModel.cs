using System.Collections.ObjectModel;
using System.Windows;
using LiteDB;
using NAudio.Wave;
using OpenSpeaker.Audio;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Services;
namespace OpenSpeaker.ViewModels;

public class VoiceGateViewModel : BaseViewModel, IDisposable
{
    private readonly VoiceGateService _service;
    private readonly DatabaseContext _db;
    private readonly AudioDeviceEnumerator _deviceEnumerator;
    private WaveInEvent? _levelMonitor;

    public ObservableCollection<AudioDeviceInfo> InputDevices { get; } = new();
    public ObservableCollection<VoiceGateProfile> Profiles { get; } = new();

    private AudioDeviceInfo? _selectedDevice;
    public AudioDeviceInfo? SelectedDevice { get => _selectedDevice; set => SetField(ref _selectedDevice, value); }

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; set => SetField(ref _isRunning, value); }

    private bool _autoStart;
    public bool AutoStart { get => _autoStart; set => SetField(ref _autoStart, value); }

    private double _volume;
    public double Volume { get => _volume; set => SetField(ref _volume, value); }

    private int _pauseThreshold = 25;
    public int PauseThreshold { get => _pauseThreshold; set => SetField(ref _pauseThreshold, value); }

    private int _resumeThreshold = 15;
    public int ResumeThreshold { get => _resumeThreshold; set => SetField(ref _resumeThreshold, value); }

    private int _resumeWaitMs = 5000;
    public int ResumeWaitMs { get => _resumeWaitMs; set => SetField(ref _resumeWaitMs, value); }

    private VoiceGateProfile? _selectedProfile;
    public VoiceGateProfile? SelectedProfile { get => _selectedProfile; set => SetField(ref _selectedProfile, value); }

    private string _newProfileName = string.Empty;
    public string NewProfileName
    {
        get => _newProfileName;
        set
        {
            if (SetField(ref _newProfileName, value))
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(System.Windows.Input.CommandManager.InvalidateRequerySuggested);
        }
    }

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand RefreshDevicesCommand { get; }
    public RelayCommand AddProfileCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }

    public VoiceGateViewModel(VoiceGateService service, DatabaseContext db, AudioDeviceEnumerator deviceEnumerator)
    {
        _service = service;
        _db = db;
        _deviceEnumerator = deviceEnumerator;

        StartCommand = new RelayCommand(Start, () => !IsRunning);
        StopCommand = new RelayCommand(Stop, () => IsRunning);
        RefreshDevicesCommand = new RelayCommand(RefreshDevices);
        AddProfileCommand = new RelayCommand(AddProfile, () => !string.IsNullOrWhiteSpace(NewProfileName));
        DeleteProfileCommand = new RelayCommand(DeleteProfile, () => SelectedProfile != null);

        RefreshDevices();
        RefreshProfiles();
        StartLevelMonitor();
    }

    private void RefreshDevices()
    {
        InputDevices.Clear();
        InputDevices.Add(new AudioDeviceInfo { Id = "-1", Name = "Default" });
        int count = WaveIn.DeviceCount;
        for (int i = 0; i < count; i++)
        {
            var caps = WaveIn.GetCapabilities(i);
            InputDevices.Add(new AudioDeviceInfo { Id = i.ToString(), Name = caps.ProductName });
        }
        SelectedDevice = InputDevices.FirstOrDefault();
    }

    private void RefreshProfiles()
    {
        Profiles.Clear();
        foreach (var p in _db.VoiceGateProfiles.FindAll())
            Profiles.Add(p);
    }

    private void Start()
    {
        if (_selectedDevice == null) return;
        var profile = new VoiceGateProfile
        {
            DeviceId = _selectedDevice.Id,
            ThresholdDb = -(100 - _pauseThreshold),
            ResumeThresholdDb = -(100 - _resumeThreshold),
            TimeoutMs = _resumeWaitMs,
            Name = "_active"
        };
        _service.Activate(profile);
        IsRunning = true;
    }

    private void Stop()
    {
        _service.Deactivate();
        IsRunning = false;
    }

    private void AddProfile()
    {
        if (string.IsNullOrWhiteSpace(NewProfileName) || _selectedDevice == null) return;
        var profile = new VoiceGateProfile
        {
            Name = NewProfileName,
            DeviceId = _selectedDevice.Id,
            ThresholdDb = -(100 - _pauseThreshold),
            ResumeThresholdDb = -(100 - _resumeThreshold),
            TimeoutMs = _resumeWaitMs,
            Enabled = true
        };
        _db.VoiceGateProfiles.Upsert(profile);
        RefreshProfiles();
        NewProfileName = string.Empty;
    }

    private void DeleteProfile()
    {
        if (_selectedProfile == null) return;
        _db.VoiceGateProfiles.Delete(_selectedProfile.Id);
        RefreshProfiles();
        SelectedProfile = null;
    }

    private void StartLevelMonitor()
    {
        try
        {
            _levelMonitor = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 100
            };
            _levelMonitor.DataAvailable += OnLevelData;
            _levelMonitor.StartRecording();
        }
        catch { }
    }

    private void OnLevelData(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;
        double sum = 0;
        int count = e.BytesRecorded / 2;
        for (int i = 0; i < e.BytesRecorded; i += 2)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i);
            double normalized = sample / 32768.0;
            sum += normalized * normalized;
        }
        double rms = count > 0 ? Math.Sqrt(sum / count) : 0;
        double pct = Math.Clamp(rms * 500, 0, 100);
        Application.Current?.Dispatcher.Invoke(() => Volume = pct);
    }

    public void Dispose()
    {
        if (_levelMonitor != null)
        {
            _levelMonitor.StopRecording();
            _levelMonitor.DataAvailable -= OnLevelData;
            _levelMonitor.Dispose();
        }
    }
}
