using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Data;
using OpenSpeaker.Infrastructure.Logging;
namespace OpenSpeaker.Api;

public class WebSocketServer : IDisposable
{
    private Fleck.WebSocketServer? _server;
    private readonly WebSocketCommandRouter _router;
    private readonly SettingsRepository _settingsRepo;
    private readonly IAppLogger _logger;
    private readonly List<IWebSocketConnection> _connections = new();
    private readonly object _connectionsLock = new();

    public bool IsRunning { get; private set; } = false;
    public event EventHandler<string>? ClientConnected;
    public event EventHandler<string>? ClientDisconnected;

    public WebSocketServer(WebSocketCommandRouter router, SettingsRepository settingsRepo, IAppLogger logger)
    {
        _router = router;
        _settingsRepo = settingsRepo;
        _logger = logger;
    }

    public void Start()
    {
        if (IsRunning) return;

        var settings = _settingsRepo.GetSettings().WebSocketServer;
        var url = $"ws://{settings.Address}:{settings.Port}";

        FleckLog.LogAction = (level, message, ex) =>
        {
            var text = $"[Fleck] {message}";
            switch (level)
            {
                case LogLevel.Debug: _logger.Debug(text); break;
                case LogLevel.Info:  _logger.Info(text);  break;
                case LogLevel.Warn:  _logger.Warn(text);  break;
                case LogLevel.Error: _logger.Error(text, ex); break;
            }
        };
        FleckLog.Level = LogLevel.Debug;

        _server = new Fleck.WebSocketServer(url);
        try
        {
        _server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                lock (_connectionsLock) { _connections.Add(socket); }

                var info = socket.ConnectionInfo;
                var headers = string.Join("; ", info.Headers.Select(h => $"{h.Key}: {h.Value}"));
                _logger.Info($"[WS CONNECT] {info.ClientIpAddress}:{info.ClientPort}  path={info.Path}  host={info.Host}  origin={info.Origin}");
                _logger.Debug($"[WS HEADERS] {headers}");

                ClientConnected?.Invoke(this, info.ClientIpAddress);
            };

            socket.OnClose = () =>
            {
                IWebSocketConnection? removed = null;
                lock (_connectionsLock)
                {
                    removed = socket;
                    _connections.Remove(socket);
                }
                var info = socket.ConnectionInfo;
                _logger.Info($"[WS DISCONNECT] {info.ClientIpAddress}:{info.ClientPort}  path={info.Path}  remaining={_connections.Count}");
                ClientDisconnected?.Invoke(this, string.Empty);
            };

            var userAgent = socket.ConnectionInfo.Headers.TryGetValue("User-Agent", out var ua) ? ua : null;

            socket.OnMessage = async message =>
            {
                _logger.Debug($"[WS RECV] {message}");
                try
                {
                    var response = await HandleMessageAsync(message, userAgent);
                    var json = JsonConvert.SerializeObject(response);
                    _logger.Debug($"[WS SEND] {json}");
                    _ = socket.Send(json);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[WS] Message handler threw for: {message}", ex);
                }
            };

            socket.OnError = ex =>
            {
                var info = socket.ConnectionInfo;
                _logger.Error($"[WS ERROR] {info.ClientIpAddress}:{info.ClientPort} - {ex.GetType().Name}: {ex.Message}", ex);
            };
        });
        }
        catch (Exception ex)
        {
            _server?.Dispose();
            _server = null;
            _logger.Warn($"[WS] Server could not bind to {url}: {ex.Message}. WebSocket commands will be unavailable.");
            return;
        }

        IsRunning = true;
        _logger.Info($"[WS] Server listening on {url}");
    }

    private async Task<ApiResponse> HandleMessageAsync(string message, string? userAgent = null)
    {
        JObject? obj;
        try { obj = JObject.Parse(message); }
        catch (Exception ex) { _logger.Warn($"[WS] Invalid JSON: {ex.Message}"); return ApiResponse.Err(string.Empty, "Invalid JSON"); }

        var requestType = obj["request"]?.Value<string>()?.ToLower() ?? string.Empty;
        var id = obj["id"]?.Value<string>() ?? string.Empty;

        BaseRequest request = requestType switch
        {
            "speak"                    => obj.ToObject<SpeakRequest>()!,
            "mode"                     => obj.ToObject<ModeRequest>()!,
            "events"                   => obj.ToObject<EventsRequest>()!,
            "subscribe"                => obj.ToObject<SubscribeRequest>()!,
            "activatevoicegateprofile" => obj.ToObject<VoiceGateProfileRequest>()!,
            _                          => new BaseRequest { Id = id, Request = obj["request"]?.Value<string>() ?? string.Empty }
        };

        return await _router.RouteAsync(request, userAgent);
    }

    public void Broadcast(string message)
    {
        List<IWebSocketConnection> snapshot;
        lock (_connectionsLock) { snapshot = new List<IWebSocketConnection>(_connections); }
        foreach (var conn in snapshot)
        {
            try
            {
                _logger.Debug($"[WS BROADCAST] {message}");
                _ = conn.Send(message);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[WS] Broadcast failed: {ex.Message}");
            }
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;

        lock (_connectionsLock)
        {
            foreach (var c in _connections)
                c.Close();
            _connections.Clear();
        }

        _server?.Dispose();
        _server = null;
        IsRunning = false;
        _logger.Info("[WS] Server stopped.");
    }

    public void Dispose() => Stop();
}
