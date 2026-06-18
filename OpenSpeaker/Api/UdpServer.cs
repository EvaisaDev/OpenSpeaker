using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Core;
using OpenSpeaker.Infrastructure.Logging;
namespace OpenSpeaker.Api;

public class UdpServer : IDisposable
{
    private UdpClient? _client;
    private readonly UdpCommandRouter _router;
    private readonly IAppLogger _logger;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public bool IsRunning { get; private set; } = false;

    public UdpServer(UdpCommandRouter router, IAppLogger logger)
    {
        _router = router;
        _logger = logger;
    }

    public void Start()
    {
        if (IsRunning) return;

        try
        {
            _cts = new CancellationTokenSource();
            _client = new UdpClient(new IPEndPoint(IPAddress.Any, Constants.UdpPort));
            IsRunning = true;
            _listenTask = Task.Run(ListenLoop);
            _logger.Info($"UDP server started on port {Constants.UdpPort}");
        }
        catch (SocketException ex)
        {
            _logger.Warn($"UDP server could not bind to port {Constants.UdpPort}: {ex.Message}. UDP commands will be unavailable.");
        }
    }

    private async Task ListenLoop()
    {
        while (!_cts!.IsCancellationRequested)
        {
            try
            {
                var result = await _client!.ReceiveAsync(_cts.Token);
                var json = Encoding.UTF8.GetString(result.Buffer);
                _ = Task.Run(() => HandleAsync(json));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error("UDP receive error", ex);
            }
        }
    }

    private async Task HandleAsync(string json)
    {
        try
        {
            var obj = JObject.Parse(json);
            var command = obj["command"]?.Value<string>()?.ToLower() ?? string.Empty;

            UdpBaseRequest request = command switch
            {
                "speak" => obj.ToObject<UdpSpeakRequest>()!,
                "events" => obj.ToObject<UdpEventsRequest>()!,
                "reg" => obj.ToObject<UdpRegRequest>()!,
                "set" => obj.ToObject<UdpSetRequest>()!,
                "assign" => obj.ToObject<UdpAssignRequest>()!,
                "profile" => obj.ToObject<UdpProfileRequest>()!,
                _ => obj.ToObject<UdpBaseRequest>()!
            };

            await _router.RouteAsync(request);
        }
        catch (Exception ex)
        {
            _logger.Error("UDP command error", ex);
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        _client?.Close();
        _client?.Dispose();
        _client = null;
        IsRunning = false;
        _logger.Info("UDP server stopped.");
    }

    public void Dispose() => Stop();
}
