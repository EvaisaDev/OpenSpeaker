using System.Diagnostics;
using System.Windows;
using OpenSpeaker.Twitch;
namespace OpenSpeaker.Views;

public partial class TwitchAuthWindow : Window
{
	private readonly TwitchAuthService _auth;
	private readonly bool _isBot;
	private readonly CancellationTokenSource _cts = new();
	private string _authUrl = string.Empty;

	public TwitchAuthWindow(TwitchAuthService auth, bool isBot = false)
	{
		InitializeComponent();
		_auth = auth;
		_isBot = isBot;
		Loaded += OnLoaded;
		Closed += OnClosed;
	}

	private async void OnLoaded(object sender, RoutedEventArgs e)
	{
		if (_isBot)
		{
			HeaderText.Text = "Log in with your bot account.";
			HintText.Text = "Make sure your browser is logged into the bot account, not your main account. A private window can help.";
		}

		try
		{
			var code = await _auth.StartDeviceLoginAsync(_isBot, _cts.Token);
			_authUrl = code.VerificationUri;
			CodeText.Text = code.UserCode;
			CodeText.Visibility = Visibility.Visible;
			CopyLinkButton.Visibility = Visibility.Visible;

			StatusText.Text = TryOpenBrowser(_authUrl, out var browserError)
				? "Approve the login in your browser. Check that the code matches the one above."
				: $"Could not open your browser ({browserError}). Copy the login link below and open it manually.";

			await _auth.CompleteDeviceLoginAsync(code, _isBot, _cts.Token);
			Close();
		}
		catch (OperationCanceledException) { }
		catch (Exception ex) { StatusText.Text = $"Error: {ex.Message}"; }
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
		if (string.IsNullOrEmpty(_authUrl)) return;
		try
		{
			System.Windows.Clipboard.SetText(_authUrl);
			StatusText.Text = "Login link copied to clipboard. Paste it into your browser to continue.";
		}
		catch (Exception ex) { StatusText.Text = $"Could not copy the link: {ex.Message}"; }
	}

	private void OnClosed(object? sender, EventArgs e) => _cts.Cancel();
	private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
