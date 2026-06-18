using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
using OpenSpeaker.Text;
namespace OpenSpeaker.Events;

public class EventProcessor : IEventProcessor
{
    private readonly EventConfigRepository _eventConfigRepo;
    private readonly WeightedMessagePicker _messagePicker;
    private readonly VariableSubstitutor _substitutor;
    private readonly ITtsQueue _queue;
    private readonly SettingsRepository _settingsRepo;

    public EventProcessor(
        EventConfigRepository eventConfigRepo,
        WeightedMessagePicker messagePicker,
        VariableSubstitutor substitutor,
        ITtsQueue queue,
        SettingsRepository settingsRepo)
    {
        _eventConfigRepo = eventConfigRepo;
        _messagePicker = messagePicker;
        _substitutor = substitutor;
        _queue = queue;
        _settingsRepo = settingsRepo;
    }

    public async Task ProcessAsync(string eventType, Dictionary<string, string> variables, string? aliasOverride = null)
    {
        await Task.Run(() =>
        {
            var settings = _settingsRepo.GetSettings();
            if (!settings.Enabled || !settings.EventsEnabled) return;

            var config = _eventConfigRepo.GetByEventType(eventType);
            if (config == null || !config.Enabled || !config.State.Enabled) return;

            if (!CheckThreshold(eventType, config.State, variables)) return;

            var message = _messagePicker.Pick(config.Messages);
            if (message == null) return;

            var text = _substitutor.Substitute(message.Template, variables);
            if (string.IsNullOrWhiteSpace(text)) return;

            var alias = aliasOverride ?? config.VoiceAliasOverride;
            if (string.IsNullOrEmpty(alias)) alias = settings.GlobalEventVoiceAlias;
            if (string.IsNullOrEmpty(alias)) alias = settings.DefaultVoiceAlias;

            _queue.Enqueue(new TtsQueueItem
            {
                Text = text,
                VoiceAliasName = alias,
                SourceEvent = eventType,
                ApplyBadWordFilter = false
            });
        });
    }

    private static bool CheckThreshold(string type, EventState state, Dictionary<string, string> vars)
    {
        if (type == EventTypes.Raid && state.MinRaidViewers > 0)
        {
            if (vars.TryGetValue("amount", out var v) && int.TryParse(v, out var viewers))
                return viewers >= state.MinRaidViewers;
        }

        if (type == EventTypes.Cheer && state.MinBits > 0)
        {
            if (vars.TryGetValue("bits", out var v) && int.TryParse(v, out var bits))
                return bits >= state.MinBits;
        }

        return true;
    }
}
