using System.Collections.ObjectModel;
using System.Windows;
using OpenSpeaker.Data;
using OpenSpeaker.Extensions;
using OpenSpeaker.Infrastructure.Logging;
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
    public Dictionary<string, VoiceInfo> VoiceById { get; } = new();
    public string VoiceCount => $"{DisplayName} Voices ({Voices.Count})";
    public void RefreshVoiceCount() => OnPropertyChanged(nameof(VoiceCount));
}

public class AliasUsageItem : BaseViewModel
{
    public VoiceAlias Alias { get; init; } = null!;
    public string Name { get; init; } = string.Empty;

    private string _voiceLabel = string.Empty;
    public string VoiceLabel { get => _voiceLabel; set => SetField(ref _voiceLabel, value); }
}

public class SpeechEnginesViewModel : BaseViewModel
{
    private readonly DatabaseContext _db;
    private readonly TtsEngineRegistry _registry;
    private readonly VoicePool _voicePool;
    private readonly ExtensionManager _extensions;
    private readonly VoiceAliasRepository _aliasRepo;
    private readonly IAppLogger? _logger;
    private readonly IDialogService _dialogs;

    public ObservableCollection<SpeechEngineItem> Engines { get; } = new();

    private SpeechEngineItem? _selectedEngine;
    public SpeechEngineItem? SelectedEngine
    {
        get => _selectedEngine;
        set
        {
            SetField(ref _selectedEngine, value);
            if (value != null)
            {
                _ = LoadVoicesAsync(value);
                _aliasSearchFilter = string.Empty;
                OnPropertyChanged(nameof(AliasSearchFilter));
                RefreshEngineAliases(value);
            }
        }
    }

    public ObservableCollection<AliasUsageItem> EngineAliases { get; } = new();
    private List<AliasUsageItem> _allEngineAliases = new();

    private string _aliasSearchFilter = string.Empty;
    public string AliasSearchFilter
    {
        get => _aliasSearchFilter;
        set { SetField(ref _aliasSearchFilter, value); ApplyAliasSearchFilter(); }
    }

    public AsyncRelayCommand ShowAddDialogCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand ReloadCommand { get; }

    public SpeechEnginesViewModel(DatabaseContext db, TtsEngineRegistry registry, VoicePool voicePool, ExtensionManager extensions, VoiceAliasRepository aliasRepo, IAppLogger? logger = null, IDialogService? dialogs = null)
    {
        _db = db;
        _registry = registry;
        _voicePool = voicePool;
        _extensions = extensions;
        _aliasRepo = aliasRepo;
        _logger = logger;
        _dialogs = dialogs ?? new DialogService();

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
            item.VoiceById.Clear();
            foreach (var v in voices)
            {
                item.Voices.Add($"{item.DisplayName} - {v.Name}");
                item.VoiceById[v.Id] = v;
            }
            item.RefreshVoiceCount();
            if (ReferenceEquals(item, SelectedEngine))
                RefreshEngineAliases(item);
        });
    }

    public void RefreshEngineAliases(SpeechEngineItem item)
    {
        var engine = _registry.GetEngine(item.EngineId);
        var voiceParamKey = engine?.GetParameters().FirstOrDefault(p => p.Type == EngineParameterType.SearchableVoice)?.Key;

        var aliases = _aliasRepo.GetAllSorted().Where(a => a.EngineId == item.EngineId).ToList();
        var usageItems = aliases.Select(a => new AliasUsageItem
        {
            Alias = a,
            Name = a.Name,
            VoiceLabel = ResolveVoiceLabel(a, item, voiceParamKey)
        }).ToList();

        _allEngineAliases = usageItems;
        ApplyAliasSearchFilter();

        if (voiceParamKey != null && engine is IVoiceSearchEngine search)
            _ = ResolveSearchableVoiceLabelsAsync(search, voiceParamKey, usageItems);
    }

    private static string ResolveVoiceLabel(VoiceAlias alias, SpeechEngineItem item, string? voiceParamKey)
    {
        if (voiceParamKey != null)
        {
            var voiceParam = SynthParams.FromJson(alias.EngineParamsJson).Str(voiceParamKey, string.Empty);
            return string.IsNullOrEmpty(voiceParam) ? "No voice selected" : "Loading...";
        }
        if (string.IsNullOrEmpty(alias.VoiceId))
            return "No voice selected";
        return item.VoiceById.TryGetValue(alias.VoiceId, out var voice) ? voice.Name : alias.VoiceId;
    }

    private static async Task ResolveSearchableVoiceLabelsAsync(IVoiceSearchEngine search, string voiceParamKey, List<AliasUsageItem> items)
    {
        var cache = new Dictionary<string, string>();
        foreach (var item in items)
        {
            var voiceParam = SynthParams.FromJson(item.Alias.EngineParamsJson).Str(voiceParamKey, string.Empty);
            if (string.IsNullOrEmpty(voiceParam)) continue;

            if (!cache.TryGetValue(voiceParam, out var label))
            {
                VoiceInfo? resolved;
                try { resolved = await search.ResolveVoiceAsync(voiceParam); }
                catch { resolved = null; }
                label = resolved?.Name ?? voiceParam;
                cache[voiceParam] = label;
            }

            var target = item;
            var resolvedLabel = label;
            Application.Current?.Dispatcher.BeginInvoke(() => target.VoiceLabel = resolvedLabel);
        }
    }

    private void ApplyAliasSearchFilter()
    {
        var filtered = string.IsNullOrEmpty(_aliasSearchFilter)
            ? _allEngineAliases
            : _allEngineAliases.Where(a => a.Name.Contains(_aliasSearchFilter, StringComparison.OrdinalIgnoreCase));
        EngineAliases.Clear();
        foreach (var a in filtered)
            EngineAliases.Add(a);
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
                _dialogs.ShowWarning($"Failed to connect: {ex.Message}", "Authentication Failed");
                return;
            }

            if (voices.Count == 0)
            {
                RevertEngineConfig(engineId);
                _dialogs.ShowWarning("The engine returned no voices, check your credentials.", "Authentication Failed");
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
