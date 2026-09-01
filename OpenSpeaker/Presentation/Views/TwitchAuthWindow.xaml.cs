using System.Diagnostics;
using System.Net;
using System.Windows;
using OpenSpeaker.Twitch;
namespace OpenSpeaker.Views;

public partial class TwitchAuthWindow : Window
{
    private readonly TwitchAuthService _auth;
    private readonly bool _isBot;
    private HttpListener? _listener;
    private string _authUrl = string.Empty;
    private const string ClientId = "o5xlrf6yir2hda9wste7lz92mu9i5w";
    private const string RedirectUri = "http://localhost:7681/callback";
    private const string Scopes = "channel:read:subscriptions channel:read:redemptions bits:read moderator:read:followers moderator:manage:banned_users user:read:chat user:write:chat";

    private const string BotScopes = "user:write:chat user:bot";

    private static readonly string CallbackHtml = """
        <!DOCTYPE html><html><head><title>OpenSpeaker Auth</title></head>
        <body style="background:#1a1a26;color:#f0f0fa;font-family:Segoe UI;text-align:center;padding-top:60px">
        <h2>Completing authentication...</h2>
        <script>
        var hash=window.location.hash.substring(1),token='';
        hash.split('&').forEach(function(p){var kv=p.split('=');if(kv[0]==='access_token')token=kv[1];});
        if(token){window.location.href='/token?t='+token;}
        else{document.body.innerHTML='<h2>Authentication failed. No token received.</h2>';}
        </script></body></html>
        """;

    private static readonly string SuccessHtml = """
        <!DOCTYPE html><html><head><title>OpenSpeaker Auth</title></head>
        <body style="background:#1a1a26;color:#f0f0fa;font-family:Segoe UI;text-align:center;padding-top:60px">
        <h2>Authentication successful!</h2><p>You can close this browser tab and return to OpenSpeaker.</p>
        </body></html>
        """;

    public TwitchAuthWindow(TwitchAuthService auth, bool isBot = false)
    {
        InitializeComponent();
        _auth = auth;
        _isBot = isBot;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:7681/");
            _listener.Start();
            _ = ListenAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not start the local login listener on port 7681 ({ex.Message}). Close any other running copy of OpenSpeaker and try again.";
            return;
        }

        var scopes = _isBot ? BotScopes : Scopes;
        var forceVerify = _isBot ? "&force_verify=true" : string.Empty;
        _authUrl = $"https://id.twitch.tv/oauth2/authorize?client_id={ClientId}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&response_type=token&scope={Uri.EscapeDataString(scopes)}{forceVerify}";

        if (!TryOpenBrowser(_authUrl, out var browserError))
        {
            StatusText.Text = $"Could not open your browser ({browserError}). Copy the login link below and open it manually.";
            CopyLinkButton.Visibility = Visibility.Visible;
        }
    }

    private static bool TryOpenBrowser(string url, out string error)
    {
        error = string.Empty;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) { error = ex.Message; }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{url}\"") { UseShellExecute = true });
            return true;
        }
        catch { }

        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c start \"\" \"{url}\"") { UseShellExecute = false, CreateNoWindow = true });
            return true;
        }
        catch { }

        return false;
    }

    private void CopyLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(_authUrl);
            StatusText.Text = "Login link copied to clipboard. Paste it into your browser to continue.";
        }
        catch (Exception ex) { StatusText.Text = $"Could not copy the link: {ex.Message}"; }
    }

    private async Task ListenAsync()
    {
        while (_listener?.IsListening == true)
        {
            HttpListenerContext? context = null;
            try { context = await _listener.GetContextAsync(); }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }

            var path = context.Request.Url?.AbsolutePath ?? string.Empty;
            if (path == "/callback" || path == "/")
                await ServeHtmlAsync(context, CallbackHtml);
            else if (path == "/token")
            {
                var token = context.Request.QueryString["t"] ?? string.Empty;
                await ServeHtmlAsync(context, SuccessHtml);
                StopListener();
                if (!string.IsNullOrEmpty(token)) await FetchAndSaveUserAsync(token);
                break;
            }
            else { context.Response.StatusCode = 404; context.Response.Close(); }
        }
    }

    private static async Task ServeHtmlAsync(HttpListenerContext context, string html)
    {
        var buffer = System.Text.Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.Close();
    }

    private async Task FetchAndSaveUserAsync(string token)
    {
        try
        {
            Dispatcher.Invoke(() => StatusText.Text = "Fetching user info...");
            using var http = new System.Net.Http.HttpClient();
            var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://api.twitch.tv/helix/users");
            request.Headers.Add("Authorization", $"Bearer {token}");
            request.Headers.Add("Client-Id", ClientId);
            var response = await http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            var obj = Newtonsoft.Json.Linq.JObject.Parse(json);
            var user = obj["data"]?[0];
            var account = new OpenSpeaker.Models.TwitchAccountInfo
            {
                AccessToken = token,
                UserId = (string?)user?["id"] ?? string.Empty,
                Login = (string?)user?["login"] ?? string.Empty,
                DisplayName = (string?)user?["display_name"] ?? string.Empty,
                BroadcasterType = (string?)user?["broadcaster_type"] ?? string.Empty,
                ProfileImageUrl = (string?)user?["profile_image_url"] ?? string.Empty,
                ClientId = ClientId
            };
            if (_isBot) _auth.SaveBotAccount(account);
            else _auth.SaveAccount(account);
            Dispatcher.Invoke(Close);
        }
        catch (Exception ex) { Dispatcher.Invoke(() => StatusText.Text = $"Error: {ex.Message}"); }
    }

    private void StopListener() { try { _listener?.Close(); } catch { } finally { _listener = null; } }
    private void OnClosed(object? sender, EventArgs e) { StopListener(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { StopListener(); Close(); }
}
