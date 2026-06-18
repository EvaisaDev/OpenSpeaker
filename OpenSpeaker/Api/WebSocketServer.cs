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

        _server = new Fleck.WebSocketServer(url);
        _server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                lock (_connectionsLock) { _connections.Add(socket); }
                _logger.Info($"WebSocket client connected: {socket.ConnectionInfo.ClientIpAddress}");
                ClientConnected?.Invoke(this, socket.ConnectionInfo.ClientIpAddress);
            };

            socket.OnClose = () =>
            {
                lock (_connectionsLock) { _connections.Remove(socket); }
                _logger.Info($"WebSocket client disconnected.");
                ClientDisconnected?.Invoke(this, string.Empty);
            };

            socket.OnMessage = async message =>
            {
                try
                {
                    var response = await HandleMessageAsync(message);
                    socket.Send(JsonConvert.SerializeObject(response));
                }
                catch (Exception ex)
                {
                    _logger.Error("WebSocket message error", ex);
                }
            };

            socket.OnError = ex => _logger.Error("WebSocket error", ex);
        });

        IsRunning = true;
        _logger.Info($"WebSocket server started on {url}{_settingsRepo.GetSettings().WebSocketServer.Endpoint}");
    }

    private async Task<ApiResponse> HandleMessageAsync(string message)
    {
        JObject? obj;
        try { obj = JObject.Parse(message); }
        catch { return ApiResponse.Err(string.Empty, "Invalid JSON"); }

        var requestType = obj["request"]?.Value<string>()?.ToLower() ?? string.Empty;
        var id = obj["id"]?.Value<string>() ?? string.Empty;

        BaseRequest request = requestType switch
        {
            "speak" => obj.ToObject<SpeakRequest>()!,
            "mode" => obj.ToObject<ModeRequest>()!,
            "events" => obj.ToObject<EventsRequest>()!,
            _ => new BaseRequest { Id = id, Request = obj["request"]?.Value<string>() ?? string.Empty }
        };

        return await _router.RouteAsync(request);
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
        _logger.Info("WebSocket server stopped.");
    }

    public void Dispose() => Stop();
}
