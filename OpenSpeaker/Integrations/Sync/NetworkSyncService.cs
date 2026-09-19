using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenSpeaker.Core;
using OpenSpeaker.Data;
using OpenSpeaker.Infrastructure.Logging;
using OpenSpeaker.Services;
namespace OpenSpeaker.Sync;

public class NetworkSyncService : IDisposable
{
    private const string DiscoverType = "openspeaker-discover";
    private const string AnnounceType = "openspeaker-instance";
    private const string PullRequest = "OPENSPEAKER-SYNC-PULL/1";
    private const int MaxPayloadBytes = 512 * 1024 * 1024;
    private const int UdpConnReset = -1744830452;

    private readonly DatabaseContext _db;
    private readonly SettingsRepository _settingsRepo;
    private readonly IAppLogger _logger;
    private readonly string _sessionToken = Guid.NewGuid().ToString("N");

    private UdpClient? _discovery;
    private TcpListener? _transfer;
    private CancellationTokenSource? _cts;

    public int TransferPort { get; private set; }
    public bool IsRunning { get; private set; }

    public NetworkSyncService(DatabaseContext db, SettingsRepository settingsRepo, IAppLogger logger)
    {
        _db = db;
        _settingsRepo = settingsRepo;
        _logger = logger;
    }

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();

        try
        {
            _transfer = new TcpListener(IPAddress.Any, 0);
            _transfer.Start();
            TransferPort = ((IPEndPoint)_transfer.LocalEndpoint).Port;
            _ = Task.Run(AcceptLoop);
        }
        catch (Exception ex)
        {
            _logger.Warn($"SYNC :: Transfer listener failed to start: {ex.Message}");
            _transfer = null;
        }

        try
        {
            _discovery = new UdpClient();
            _discovery.ExclusiveAddressUse = false;
            _discovery.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _discovery.Client.Bind(new IPEndPoint(IPAddress.Any, Constants.SyncDiscoveryPort));
            _discovery.EnableBroadcast = true;
            DisableConnReset(_discovery);
            try { _discovery.JoinMulticastGroup(IPAddress.Parse(Constants.SyncMulticastAddress)); }
            catch (Exception ex) { _logger.Debug($"SYNC :: Multicast join failed: {ex.Message}"); }
            _ = Task.Run(DiscoveryLoop);
        }
        catch (Exception ex)
        {
            _logger.Warn($"SYNC :: Discovery listener failed to bind port {Constants.SyncDiscoveryPort}: {ex.Message}");
            _discovery = null;
        }

        IsRunning = true;
        _logger.Info($"SYNC :: Network sync ready (transfer port {TransferPort})");
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _cts?.Cancel();
        try { _discovery?.Close(); } catch { }
        try { _transfer?.Stop(); } catch { }
        _discovery = null;
        _transfer = null;
    }

    private static void DisableConnReset(UdpClient client)
    {
        try { client.Client.IOControl((IOControlCode)UdpConnReset, new byte[] { 0, 0, 0, 0 }, null); }
        catch { }
    }

    private async Task DiscoveryLoop()
    {
        var client = _discovery!;
        var token = _cts!.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await client.ReceiveAsync(token);
                HandleDiscoveryDatagram(client, result);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { }
            catch (Exception ex) { _logger.Debug($"SYNC :: Discovery receive error: {ex.Message}"); }
        }
    }

    private void HandleDiscoveryDatagram(UdpClient client, UdpReceiveResult result)
    {
        JObject msg;
        try { msg = JObject.Parse(Encoding.UTF8.GetString(result.Buffer)); }
        catch { return; }

        if (msg["type"]?.ToString() != DiscoverType) return;
        if (msg["session"]?.ToString() == _sessionToken) return;
        if (_transfer == null) return;

        var settings = _settingsRepo.GetSettings();
        var reply = JsonConvert.SerializeObject(new
        {
            type = AnnounceType,
            session = _sessionToken,
            instanceId = settings.InstanceId,
            instanceName = settings.InstanceName,
            version = UpdateService.CurrentVersion,
            port = TransferPort
        });
        var bytes = Encoding.UTF8.GetBytes(reply);
        try { client.Send(bytes, bytes.Length, result.RemoteEndPoint); }
        catch (Exception ex) { _logger.Debug($"SYNC :: Discovery reply failed: {ex.Message}"); }
    }

    private async Task AcceptLoop()
    {
        var listener = _transfer!;
        var token = _cts!.Token;
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(token); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception ex) { _logger.Debug($"SYNC :: Accept error: {ex.Message}"); continue; }
            _ = Task.Run(() => ServeClientAsync(client));
        }
    }

    private async Task ServeClientAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 10000;
                var stream = client.GetStream();
                var request = await ReadLineAsync(stream);
                if (request != PullRequest) return;

                var remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                _logger.Info($"SYNC :: Sending database snapshot to {remote}");
                var json = DatabaseSnapshot.Export(_db, UpdateService.CurrentVersion);
                await WriteFramedAsync(stream, json);
            }
            catch (Exception ex)
            {
                _logger.Warn($"SYNC :: Failed to serve snapshot: {ex.Message}");
            }
        }
    }

    public async Task<List<SyncInstanceInfo>> DiscoverAsync(TimeSpan timeout, CancellationToken cancel = default)
    {
        var found = new Dictionary<string, SyncInstanceInfo>();
        using var probe = new UdpClient();
        probe.ExclusiveAddressUse = false;
        probe.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        probe.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        probe.EnableBroadcast = true;
        probe.MulticastLoopback = true;
        DisableConnReset(probe);

        var request = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { type = DiscoverType, session = _sessionToken }));
        foreach (var target in DiscoveryTargets())
        {
            try { await probe.SendAsync(request, request.Length, target); }
            catch (Exception ex) { _logger.Debug($"SYNC :: Probe to {target} failed: {ex.Message}"); }
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeoutCts.CancelAfter(timeout);
        while (!timeoutCts.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try { result = await probe.ReceiveAsync(timeoutCts.Token); }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { continue; }

            var info = ParseAnnounce(result);
            if (info != null && !found.ContainsKey(info.SessionToken))
                found[info.SessionToken] = info;
        }

        return found.Values.OrderBy(i => i.InstanceName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private SyncInstanceInfo? ParseAnnounce(UdpReceiveResult result)
    {
        try
        {
            var msg = JObject.Parse(Encoding.UTF8.GetString(result.Buffer));
            if (msg["type"]?.ToString() != AnnounceType) return null;
            var session = msg["session"]?.ToString() ?? string.Empty;
            if (session == _sessionToken) return null;
            var port = msg["port"]?.Value<int>() ?? 0;
            if (port <= 0) return null;
            return new SyncInstanceInfo(
                session,
                msg["instanceId"]?.ToString() ?? string.Empty,
                msg["instanceName"]?.ToString() ?? "OpenSpeaker",
                msg["version"]?.ToString() ?? string.Empty,
                result.RemoteEndPoint.Address.ToString(),
                port);
        }
        catch { return null; }
    }

    private static IEnumerable<IPEndPoint> DiscoveryTargets()
    {
        var targets = new List<IPEndPoint>
        {
            new(IPAddress.Broadcast, Constants.SyncDiscoveryPort),
            new(IPAddress.Parse(Constants.SyncMulticastAddress), Constants.SyncDiscoveryPort),
            new(IPAddress.Loopback, Constants.SyncDiscoveryPort)
        };

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork || addr.IPv4Mask == null) continue;
                    var ip = addr.Address.GetAddressBytes();
                    var mask = addr.IPv4Mask.GetAddressBytes();
                    var bc = new byte[4];
                    for (var i = 0; i < 4; i++) bc[i] = (byte)(ip[i] | ~mask[i]);
                    targets.Add(new IPEndPoint(new IPAddress(bc), Constants.SyncDiscoveryPort));
                }
            }
        }
        catch { }

        return targets.DistinctBy(t => t.ToString());
    }

    public async Task<string> PullAsync(SyncInstanceInfo instance, IProgress<long>? bytesProgress = null, CancellationToken cancel = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(instance.Host, instance.Port, cancel);
        var stream = client.GetStream();

        var request = Encoding.UTF8.GetBytes(PullRequest + "\n");
        await stream.WriteAsync(request, cancel);
        await stream.FlushAsync(cancel);

        return await ReadFramedAsync(stream, bytesProgress, cancel);
    }

    private static async Task<string> ReadLineAsync(NetworkStream stream)
    {
        var sb = new StringBuilder();
        var buf = new byte[1];
        while (sb.Length < 256)
        {
            var n = await stream.ReadAsync(buf, 0, 1);
            if (n == 0) break;
            if (buf[0] == (byte)'\n') break;
            sb.Append((char)buf[0]);
        }
        return sb.ToString().TrimEnd('\r');
    }

    private static async Task WriteFramedAsync(NetworkStream stream, string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var header = new byte[8];
        BitConverter.TryWriteBytes(header, (long)body.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(body);
        await stream.FlushAsync();
    }

    private static async Task<string> ReadFramedAsync(NetworkStream stream, IProgress<long>? progress, CancellationToken cancel)
    {
        var header = await ReadExactAsync(stream, 8, cancel);
        var length = BitConverter.ToInt64(header);
        if (length <= 0 || length > MaxPayloadBytes)
            throw new InvalidOperationException("Invalid sync payload size.");

        var body = new byte[length];
        var read = 0;
        while (read < length)
        {
            var n = await stream.ReadAsync(body.AsMemory(read, (int)(length - read)), cancel);
            if (n == 0) throw new EndOfStreamException("Connection closed before the sync payload was fully received.");
            read += n;
            progress?.Report(read);
        }
        return Encoding.UTF8.GetString(body);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, CancellationToken cancel)
    {
        var buf = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = await stream.ReadAsync(buf.AsMemory(read, count - read), cancel);
            if (n == 0) throw new EndOfStreamException("Connection closed unexpectedly.");
            read += n;
        }
        return buf;
    }

    public void Dispose() => Stop();
}
