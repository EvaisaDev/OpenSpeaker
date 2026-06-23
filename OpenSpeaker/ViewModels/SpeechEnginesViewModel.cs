using System.Collections.ObjectModel;
using System.Windows;
using OpenSpeaker.Data;
using OpenSpeaker.Extensions;
using OpenSpeaker.ThingsIDKWhereToPut.Logging;
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
    private readonly ExtensionManager _extensions;
    private readonly IAppLogger? _logger;

    public ObservableCollection<SpeechEngineItem> Engines { get; } = new();

    private SpeechEngineItem? _selectedEngine;
    public SpeechEngineItem? SelectedEngine
    {
        get => _selectedEngine;
        set { SetField(ref _selectedEngine, value); if (value != null) _ = LoadVoicesAsync(value); }
    }

    public AsyncRelayCommand ShowAddDialogCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand ReloadCommand { get; }

    public SpeechEnginesViewModel(DatabaseContext db, TtsEngineRegistry registry, VoicePool voicePool, ExtensionManager extensions, IAppLogger? logger = null)
    {
        _db = db;
        _registry = registry;
        _voicePool = voicePool;
        _extensions = extensions;
        _logger = logger;

        ShowAddDialogCommand = new AsyncRelayCommand(ShowAddDialog);
        RemoveCommand = new RelayCommand(RemoveEngine, () => SelectedEngine != null);
        ReloadCommand = new RelayCommand(ReloadEngines);

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
            var displayName = cfg.EngineId.StartsWith("ext:", StringComparison.Ordinal)
                ? _extensions.GetDisplayName(cfg.EngineId)
                : EngineConfigViewModel.GetDisplayName(cfg.EngineId);

            var item = new SpeechEngineItem { EngineId = cfg.EngineId, DisplayName = displayName };
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

        IReadOnlyList<VoiceInfo> voices;
        try
        {
            voices = await engine.GetVoicesAsync().ConfigureAwait(false);
        }
        catch
        {
            if (item.EngineId != EngineIds.Sapi5 && !item.EngineId.StartsWith("ext:", StringComparison.Ordinal))
                MarkEngineFailed(item);
            return;
        }

        if (voices.Count == 0 && item.EngineId != EngineIds.Sapi5 && !item.EngineId.StartsWith("ext:", StringComparison.Ordinal))
        {
            MarkEngineFailed(item);
            return;
        }

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            item.Voices.Clear();
            foreach (var v in voices)
                item.Voices.Add($"{item.DisplayName} - {v.Name}");
            item.RefreshVoiceCount();
        });
    }

    private void MarkEngineFailed(SpeechEngineItem item)
    {
        var cfg = _db.EngineConfigs.FindOne(c => c.EngineId == item.EngineId);
        if (cfg != null)
        {
            cfg.Enabled = false;
            _db.EngineConfigs.Upsert(cfg);
        }
        _voicePool.Invalidate();
        Application.Current?.Dispatcher.BeginInvoke(() => Engines.Remove(item));
    }

    private async Task ShowAddDialog()
    {
        var alreadyAdded = Engines.Select(e => e.EngineId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var options = TtsEngineFactory.BuiltIn
            .Where(d => d.Id != EngineIds.Sapi5 && !alreadyAdded.Contains(d.Id))
            .Select(d => new EngineOption(d.Id, d.DisplayName))
            .Concat(_extensions.SpeechEngines
                .Where(e => !alreadyAdded.Contains(e.EngineId))
                .Select(e => new EngineOption(e.EngineId, e.DisplayName)))
            .ToList();

        if (options.Count == 0) return;

        EngineConfigViewModel? vm = null;
        Window? dialog = null;
        vm = new EngineConfigViewModel(options, () => dialog?.Close(), _extensions);
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

        var engine = _registry.GetEngine(engineId);
        if (engine != null)
        {
            IReadOnlyList<VoiceInfo> voices;
            try { voices = await engine.GetVoicesAsync(); }
            catch (Exception ex)
            {
                RevertEngineConfig(engineId);
                MessageBox.Show($"Failed to connect: {ex.Message}", "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (voices.Count == 0)
            {
                RevertEngineConfig(engineId);
                MessageBox.Show("The engine returned no voices — check your credentials.", "Authentication Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        _voicePool.Invalidate();
        Refresh();
    }

    private void RevertEngineConfig(string engineId)
    {
        var cfg = _db.EngineConfigs.FindOne(c => c.EngineId == engineId);
        if (cfg == null) return;
        cfg.Enabled = false;
        _db.EngineConfigs.Upsert(cfg);
        _registry.Reload();
    }

    private void ReloadEngines()
    {
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
