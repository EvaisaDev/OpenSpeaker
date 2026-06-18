using System.Collections.ObjectModel;
using System.Windows;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Services;
using OpenSpeaker.TTS;
using OpenSpeaker.Views;
namespace OpenSpeaker.ViewModels;

public class SpeechEngineItem : BaseViewModel
{
    public string EngineId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public ObservableCollection<string> Voices { get; } = new();
    public string VoiceCount => $"{DisplayName} Voices ({Voices.Count})";
    public void RefreshVoiceCount() => OnPropertyChanged(nameof(VoiceCount));
}

public class SpeechEnginesViewModel : BaseViewModel
{
    private readonly DatabaseContext _db;
    private readonly TtsEngineRegistry _registry;
    private readonly VoicePool _voicePool;

    public ObservableCollection<SpeechEngineItem> Engines { get; } = new();

    private SpeechEngineItem? _selectedEngine;
    public SpeechEngineItem? SelectedEngine
    {
        get => _selectedEngine;
        set { SetField(ref _selectedEngine, value); if (value != null) _ = LoadVoicesAsync(value); }
    }

    public RelayCommand ShowAddDialogCommand { get; }
    public RelayCommand RemoveCommand { get; }

    private static readonly string[] _availableToAdd =
    [
        EngineIds.Azure, EngineIds.AmazonPolly, EngineIds.GoogleCloud,
        EngineIds.ElevenLabs, EngineIds.TtsMonster, EngineIds.IbmWatson,
        EngineIds.Acapela, EngineIds.CereProc, EngineIds.UberDuck, EngineIds.TikTok
    ];

    public SpeechEnginesViewModel(DatabaseContext db, TtsEngineRegistry registry, VoicePool voicePool)
    {
        _db = db;
        _registry = registry;
        _voicePool = voicePool;

        ShowAddDialogCommand = new RelayCommand(ShowAddDialog);
        RemoveCommand = new RelayCommand(RemoveEngine, () => SelectedEngine != null);

        Refresh();
    }

    public void Refresh()
    {
        Engines.Clear();
        var enabled = _db.EngineConfigs.FindAll().Where(c => c.Enabled).ToList();

        var sapi5 = new SpeechEngineItem { EngineId = EngineIds.Sapi5, DisplayName = EngineConfigViewModel.GetDisplayName(EngineIds.Sapi5) };
        Engines.Add(sapi5);
        _ = LoadVoicesAsync(sapi5);

        foreach (var cfg in enabled.Where(c => c.EngineId != EngineIds.Sapi5))
        {
            var item = new SpeechEngineItem
            {
                EngineId = cfg.EngineId,
                DisplayName = EngineConfigViewModel.GetDisplayName(cfg.EngineId)
            };
            Engines.Add(item);
            _ = LoadVoicesAsync(item);
        }

        foreach (var api in _db.CustomApis.FindAll().Where(a => a.Enabled))
        {
            var item = new SpeechEngineItem { EngineId = api.EngineId, DisplayName = api.Name };
            Engines.Add(item);
            _ = LoadVoicesAsync(item);
        }

        SelectedEngine = Engines.FirstOrDefault();
    }

    private async Task LoadVoicesAsync(SpeechEngineItem item)
    {
        var engine = _registry.GetEngine(item.EngineId);
        if (engine == null) return;

        try
        {
            var voices = await engine.GetVoicesAsync();
            Application.Current?.Dispatcher.Invoke(() =>
            {
                item.Voices.Clear();
                foreach (var v in voices)
                    item.Voices.Add($"{item.DisplayName} - {v.Name}");
                item.RefreshVoiceCount();
            });
        }
        catch { }
    }

    private void ShowAddDialog()
    {
        EngineConfigViewModel? vm = null;
        Window? dialog = null;
        vm = new EngineConfigViewModel(_availableToAdd, () => dialog?.Close());
        dialog = new EngineConfigWindow(vm) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();

        if (!vm.Confirmed) return;

        var engineId = vm.SelectedEngine!.Id;
        var configJson = vm.BuildConfigJson();

        var existing = _db.EngineConfigs.FindOne(c => c.EngineId == engineId);
        if (existing != null)
        {
            existing.Enabled = true;
            existing.ConfigJson = configJson;
            _db.EngineConfigs.Upsert(existing);
        }
        else
        {
            _db.EngineConfigs.Insert(new EngineConfig { EngineId = engineId, Enabled = true, ConfigJson = configJson });
        }

        _registry.Reload();
        _voicePool.Invalidate();
        Refresh();
    }

    private void RemoveEngine()
    {
        if (_selectedEngine == null || _selectedEngine.EngineId == EngineIds.Sapi5) return;
        if (_selectedEngine.EngineId.StartsWith("custom:")) return;
        var existing = _db.EngineConfigs.FindOne(c => c.EngineId == _selectedEngine.EngineId);
        if (existing != null)
        {
            existing.Enabled = false;
            _db.EngineConfigs.Upsert(existing);
        }
        _registry.Reload();
        _voicePool.Invalidate();
        Refresh();
    }
}
