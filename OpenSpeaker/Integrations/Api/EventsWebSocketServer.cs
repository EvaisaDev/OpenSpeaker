using Fleck;
using Newtonsoft.Json;
using OpenSpeaker.Data;
using OpenSpeaker.Infrastructure.Logging;
namespace OpenSpeaker.Api;

public class EventsWebSocketServer : IDisposable
{
    private Fleck.WebSocketServer? _server;
    private readonly WebSocketCommandRouter _router;
    private readonly SettingsRepository _settingsRepo;
    private readonly IAppLogger _logger;
    private readonly List<IWebSocketConnection> _connections = new();
    private readonly object _connectionsLock = new();

    public bool IsRunning { get; private set; } = false;

    public EventsWebSocketServer(WebSocketCommandRouter router, SettingsRepository settingsRepo, IAppLogger logger)
    {
        _router = router;
        _settingsRepo = settingsRepo;
        _logger = logger;
    }

    public void Start()
    {
        if (IsRunning) return;

        var settings = _settingsRepo.GetSettings().EventsWebSocketServer;
        var url = $"ws://{settings.Address}:{settings.Port}";

        _server = new Fleck.WebSocketServer(url);
        try
        {
            _server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    lock (_connectionsLock) { _connections.Add(socket); }
                    var info = socket.ConnectionInfo;
                    _logger.Info($"[EVENTS WS CONNECT] {info.ClientIpAddress}:{info.ClientPort}");
                };

                socket.OnClose = () =>
                {
                    lock (_connectionsLock) { _connections.Remove(socket); }
                    var info = socket.ConnectionInfo;
                    _logger.Info($"[EVENTS WS DISCONNECT] {info.ClientIpAddress}:{info.ClientPort}");
                };

                socket.OnMessage = async message =>
                {
                    _logger.Debug($"[EVENTS WS RECV] {message}");
                    try
                    {
                        var response = await HandleMessageAsync(message);
                        var json = JsonConvert.SerializeObject(response);
                        _logger.Debug($"[EVENTS WS SEND] {json}");
                        _ = socket.Send(json);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[EVENTS WS] Message handler threw for: {message}", ex);
                    }
                };

                socket.OnError = ex =>
                {
                    var info = socket.ConnectionInfo;
                    _logger.Error($"[EVENTS WS ERROR] {info.ClientIpAddress}:{info.ClientPort} - {ex.GetType().Name}: {ex.Message}", ex);
                };
            });
        }
        catch (Exception ex)
        {
            _server?.Dispose();
            _server = null;
            _logger.Warn($"[EVENTS WS] Server could not bind to {url}: {ex.Message}. Events WebSocket will be unavailable.");
            return;
        }

        IsRunning = true;
        _logger.Info($"[EVENTS WS] Server listening on {url}");
    }

    private async Task<ApiResponse> HandleMessageAsync(string message)
    {
        BaseRequest request;
        try { request = WebSocketCommandRouter.ParseRequest(message); }
        catch (Exception ex) { _logger.Warn($"[EVENTS WS] Invalid JSON: {ex.Message}"); return ApiResponse.Err(string.Empty, "Invalid JSON"); }

        return await _router.RouteAsync(request);
    }

    public void Broadcast(string message)
    {
        List<IWebSocketConnection> snapshot;
        lock (_connectionsLock) { snapshot = new List<IWebSocketConnection>(_connections); }
        foreach (var conn in snapshot)
        {
            try
            {
                _ = conn.Send(message);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[EVENTS WS] Broadcast failed: {ex.Message}");
            }
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;

        List<IWebSocketConnection> snapshot;
        lock (_connectionsLock)
        {
            snapshot = new List<IWebSocketConnection>(_connections);
            _connections.Clear();
        }
        foreach (var c in snapshot)
            c.Close();

        _server?.Dispose();
        _server = null;
        IsRunning = false;
        _logger.Info("[EVENTS WS] Server stopped.");
    }

    public void Dispose() => Stop();
}
