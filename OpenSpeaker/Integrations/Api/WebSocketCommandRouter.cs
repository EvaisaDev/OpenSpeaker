using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
using OpenSpeaker.Services;
using System.Text.RegularExpressions;
namespace OpenSpeaker.Api;

public class WebSocketCommandRouter
{
    private readonly ITtsOrchestrator _orchestrator;
    private readonly ITtsQueue _queue;
    private readonly SettingsRepository _settingsRepo;
    private readonly DatabaseContext _db;
    private readonly VoiceGateService _voiceGate;
    private readonly string _identityName;
    private readonly string _identityVersion;

    public Action<string>? Broadcast { get; set; }

    private static readonly string[] AllCommands =
    [
        "Speak", "Pause", "Resume", "Clear", "Stop", "Off", "Disable", "On", "Enable",
        "Events", "Mode", "GetEvent", "Subscribe", "Unsubscribe", "GetInfo",
        "GetAliases", "GetState", "GetVoiceGateProfiles", "ActivateVoiceGateProfile"
    ];

    public WebSocketCommandRouter(ITtsOrchestrator orchestrator, ITtsQueue queue, SettingsRepository settingsRepo, DatabaseContext db, VoiceGateService voiceGate, string? identityName = null, string? identityVersion = null)
    {
        _orchestrator = orchestrator;
        _queue = queue;
        _settingsRepo = settingsRepo;
        _db = db;
        _voiceGate = voiceGate;
        _identityName = identityName ?? "Speaker.bot";
        _identityVersion = identityVersion ?? "0.1.6";
    }

    public static BaseRequest ParseRequest(string message)
    {
        var obj = JObject.Parse(message);
        var requestType = obj["request"]?.Value<string>()?.ToLower() ?? string.Empty;
        var id = obj["id"]?.Value<string>() ?? string.Empty;

        return requestType switch
        {
            "speak"                    => obj.ToObject<SpeakRequest>()!,
            "mode"                     => obj.ToObject<ModeRequest>()!,
            "events"                   => obj.ToObject<EventsRequest>()!,
            "subscribe"                => obj.ToObject<SubscribeRequest>()!,
            "activatevoicegateprofile" => obj.ToObject<VoiceGateProfileRequest>()!,
            _                          => new BaseRequest { Id = id, Request = obj["request"]?.Value<string>() ?? string.Empty }
        };
    }

    public async Task<ApiResponse> RouteAsync(BaseRequest request, string? userAgent = null)
    {
        try
        {
            switch (request.Request.ToLower())
            {
                case "commands":
                    return ApiResponse.WithResult(request.Id, new { commands = AllCommands });

                case "getinfo":
                    var infoSettings = _settingsRepo.GetSettings();
                    return ApiResponse.WithResult(request.Id, new
                    {
                        instanceId = infoSettings.InstanceId,
                        name = _identityName, // Legacy server keeps pretending to be Speaker.bot so Streamer.bot accepts it as a Speaker.bot integration.
                        version = _identityVersion,
                        os = "windows",
                        apiVersion = 2
                    });

                case "subscribe":
                    var subReq = request as SubscribeRequest;
                    return ApiResponse.WithResult(request.Id, new { events = subReq?.Events ?? new Newtonsoft.Json.Linq.JObject() });

                case "unsubscribe":
                    return ApiResponse.Ok(request.Id);

                case "getevent":
                    return ApiResponse.Ok(request.Id);

                case "getstate":
                    var stateSettings = _settingsRepo.GetSettings();
                    return ApiResponse.WithResult(request.Id, new
                    {
                        enabled = _orchestrator.IsEnabled,
                        events = stateSettings.EventsEnabled,
                        paused = _queue.IsPaused,
                        speaking = false
                    });

                case "getaliases":
                    var aliases = _db.VoiceAliases.FindAll()
                        .Select(a => new { name = a.Name, voice = a.VoiceId, engine = a.EngineId })
                        .ToList();
                    return ApiResponse.WithResult(request.Id, new { aliases });

                case "speak":
                    var speakReq = request as SpeakRequest ?? new SpeakRequest { Id = request.Id, Request = request.Request };
                    var alias = _db.VoiceAliases.FindOne(a => a.Name == speakReq.Voice);
                    if (alias == null)
                        return ApiResponse.Err(request.Id, "Voice alias not found");
                    string? speechId = null;
                    await _orchestrator.SpeakAsync(speakReq.Message, speakReq.Voice, speakReq.BadWordFilter, speakReq.Silent, delay: false, onQueued: id => speechId = id);
                    return ApiResponse.WithResult(request.Id, new
                    {
                        speechId,
                        text = speakReq.Message,
                        voiceName = alias.VoiceId,
                        pitch = 1.0,
                        volume = 1.0,
                        rate = 0.0
                    });

                case "stop":
                    _orchestrator.Stop();
                    return ApiResponse.Ok(request.Id);

                case "on":
                case "enable":
                    _orchestrator.SetEnabled(true);
                    return ApiResponse.Ok(request.Id);

                case "off":
                case "disable":
                    _orchestrator.SetEnabled(false);
                    return ApiResponse.Ok(request.Id);

                case "pause":
                    _orchestrator.Pause();
                    return ApiResponse.Ok(request.Id);

                case "resume":
                    _orchestrator.Resume();
                    return ApiResponse.Ok(request.Id);

                case "clear":
                    _orchestrator.Clear();
                    return ApiResponse.Ok(request.Id);

                case "events":
                    var eventsReq = request as EventsRequest;
                    if (eventsReq != null)
                    {
                        var enabled = eventsReq.State.ToLower() == "on";
                        _settingsRepo.Update(s => s.EventsEnabled = enabled);
                    }
                    return ApiResponse.Ok(request.Id);

                case "mode":
                    var modeReq = request as ModeRequest;
                    if (modeReq != null)
                    {
                        if (modeReq.Mode.ToLower() == "command")
                        {
                            if (_settingsRepo.GetSettings().TtsCommands.Count == 0)
                                return ApiResponse.Err(request.Id, "No TTS commands configured.");
                            _settingsRepo.Update(s => s.Mode = TtsModes.Command);
                        }
                        else
                        {
                            _settingsRepo.Update(s => s.Mode = TtsModes.Everything);
                        }
                    }
                    return ApiResponse.Ok(request.Id);

                case "getvoicegateprofiles":
                    var profiles = _db.VoiceGateProfiles.FindAll()
                        .Select(p => new { id = p.Id.ToString(), name = p.Name })
                        .ToList();
                    return ApiResponse.WithResult(request.Id, new { profiles });

                case "activatevoicegateprofile":
                    var vgReq = request as VoiceGateProfileRequest;
                    if (vgReq?.Profile == null)
                        return ApiResponse.Err(request.Id, "Missing profile.");
                    if (!TryParseGuid(vgReq.Profile.Id, out var profileGuid))
                        return ApiResponse.Err(request.Id, "Invalid profile id.");
                    var profile = _db.VoiceGateProfiles.FindById(profileGuid);
                    if (profile == null)
                        return ApiResponse.Err(request.Id, "Profile not found.");
                    _voiceGate.Activate(profile);
                    BroadcastProfileActivated(profile);
                    return ApiResponse.Ok(request.Id);

                default:
                    return ApiResponse.Err(request.Id, $"Unknown command: {request.Request}");
            }
        }
        catch (Exception ex)
        {
            return ApiResponse.Err(request.Id, ex.Message);
        }
    }

    private void BroadcastProfileActivated(VoiceGateProfile p)
    {
        var payload = new
        {
            timeStamp = DateTime.Now.ToString("O"),
            @event = new { source = "VoiceGate", type = "ProfileActivated" },
            data = new { profile = MapProfile(p) }
        };
        Broadcast?.Invoke(JsonConvert.SerializeObject(payload));
    }


    private static object MapProfile(VoiceGateProfile p) => new
    {
        id = p.Id.ToString(),
        device = Guid.Empty.ToString(),
        name = p.Name,
        pauseThreshold = Math.Max(0f, (100f + p.ThresholdDb) / 100f),
        resumeThreshold = Math.Max(0f, (100f + p.ResumeThresholdDb) / 100f),
        resumeWaitTime = (float)p.TimeoutMs
    };

    private static bool TryParseGuid(string input, out Guid guid)
    {
        if (Guid.TryParse(input, out guid)) return true;

        var m = Regex.Match(input,
            @"\{0x([0-9a-fA-F]{8}),0x([0-9a-fA-F]{4}),0x([0-9a-fA-F]{4}),\{0x([0-9a-fA-F]{2}),0x([0-9a-fA-F]{2}),0x([0-9a-fA-F]{2}),0x([0-9a-fA-F]{2}),0x([0-9a-fA-F]{2}),0x([0-9a-fA-F]{2}),0x([0-9a-fA-F]{2}),0x([0-9a-fA-F]{2})\}\}");
        if (m.Success)
        {
            var s = $"{m.Groups[1].Value}-{m.Groups[2].Value}-{m.Groups[3].Value}-{m.Groups[4].Value}{m.Groups[5].Value}-{m.Groups[6].Value}{m.Groups[7].Value}{m.Groups[8].Value}{m.Groups[9].Value}{m.Groups[10].Value}{m.Groups[11].Value}";
            if (Guid.TryParse(s, out guid)) return true;
        }

        guid = Guid.Empty;
        return false;
    }
}
