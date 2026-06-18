using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
using OpenSpeaker.Services;
namespace OpenSpeaker.Api;

public class WebSocketCommandRouter
{
    private readonly ITtsOrchestrator _orchestrator;
    private readonly SettingsRepository _settingsRepo;

    public WebSocketCommandRouter(ITtsOrchestrator orchestrator, SettingsRepository settingsRepo)
    {
        _orchestrator = orchestrator;
        _settingsRepo = settingsRepo;
    }

    public async Task<ApiResponse> RouteAsync(BaseRequest request)
    {
        try
        {
            switch (request.Request.ToLower())
            {
                case "speak":
                    var speakReq = request as SpeakRequest ?? new SpeakRequest { Id = request.Id, Request = request.Request };
                    await _orchestrator.SpeakAsync(speakReq.Message, speakReq.Voice, speakReq.BadWordFilter, speakReq.Silent, speakReq.Delay);
                    return ApiResponse.Ok(request.Id);

                case "stop":
                    _orchestrator.Stop();
                    return ApiResponse.Ok(request.Id);

                case "enable":
                    _orchestrator.SetEnabled(true);
                    return ApiResponse.Ok(request.Id);

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
                        var settings = _settingsRepo.GetSettings();
                        settings.EventsEnabled = eventsReq.State.ToLower() == "on";
                        _settingsRepo.SaveSettings(settings);
                    }
                    return ApiResponse.Ok(request.Id);

                case "mode":
                    var modeReq = request as ModeRequest;
                    if (modeReq != null)
                    {
                        if (modeReq.Mode.ToLower() == "command")
                        {
                            var settings = _settingsRepo.GetSettings();
                            if (settings.TtsCommands.Count == 0)
                                return ApiResponse.Err(request.Id, "No TTS commands configured.");
                            settings.Mode = TtsModes.Command;
                            _settingsRepo.SaveSettings(settings);
                        }
                        else
                        {
                            var settings = _settingsRepo.GetSettings();
                            settings.Mode = TtsModes.Everything;
                            _settingsRepo.SaveSettings(settings);
                        }
                    }
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
}
