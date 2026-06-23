using OpenSpeaker.Data;
using OpenSpeaker.Queue;
using OpenSpeaker.Services;
using OpenSpeaker.Users;
namespace OpenSpeaker.Api;

public class UdpCommandRouter
{
    private readonly ITtsOrchestrator _orchestrator;
    private readonly ITtsQueue _queue;
    private readonly UserService _userService;
    private readonly SettingsRepository _settingsRepo;
    private readonly VoiceGateService _voiceGateService;
    private readonly EventConfigRepository _eventConfigRepo;

    public UdpCommandRouter(
        ITtsOrchestrator orchestrator,
        ITtsQueue queue,
        UserService userService,
        SettingsRepository settingsRepo,
        VoiceGateService voiceGateService,
        EventConfigRepository eventConfigRepo)
    {
        _orchestrator = orchestrator;
        _queue = queue;
        _userService = userService;
        _settingsRepo = settingsRepo;
        _voiceGateService = voiceGateService;
        _eventConfigRepo = eventConfigRepo;
    }

    public async Task RouteAsync(UdpBaseRequest request)
    {
        switch (request.Command.ToLower())
        {
            case "speak":
                var speakReq = request as UdpSpeakRequest;
                if (speakReq != null)
                    await _orchestrator.SpeakAsync(speakReq.Message, speakReq.Voice);
                break;

            case "stop":
                _orchestrator.Stop();
                break;

            case "enable":
            case "on":
                _orchestrator.SetEnabled(true);
                break;

            case "disable":
            case "off":
                _orchestrator.SetEnabled(false);
                break;

            case "pause":
                _orchestrator.Pause();
                break;

            case "resume":
                _orchestrator.Resume();
                break;

            case "clear":
                _orchestrator.Clear();
                break;

            case "events":
                var eventsReq = request as UdpEventsRequest;
                if (eventsReq != null)
                {
                    var enabled = eventsReq.State.ToLower() == "on";
                    _settingsRepo.Update(s => s.EventsEnabled = enabled);
                }
                break;

            case "profile":
                var profileReq = request as UdpProfileRequest;
                if (profileReq != null)
                    _voiceGateService.ActivateProfile(profileReq.Profile);
                break;

            case "reg":
                var regReq = request as UdpRegRequest;
                if (regReq != null)
                    await _userService.SetRegularAsync(regReq.User, regReq.Mode.ToLower() == "add");
                break;

            case "set":
                var setReq = request as UdpSetRequest;
                if (setReq != null)
                {
                    if (setReq.Method.ToLower() == "nickname")
                        await _userService.SetNicknameAsync(setReq.Username, setReq.Nickname);
                }
                break;

            case "assign":
                var assignReq = request as UdpAssignRequest;
                if (assignReq != null && assignReq.Method.ToLower() == "last")
                {
                    var (voiceId, engineId) = _queue.LastUsedVoice;
                    if (!string.IsNullOrEmpty(voiceId))
                        await _userService.AssignLastVoiceAsync(assignReq.Username, voiceId, engineId);
                }
                break;
        }
    }
}
