using System.Windows;
using System.Windows.Controls;
using OpenSpeaker.Core;
using OpenSpeaker.Localization;
using OpenSpeaker.Services;
using OpenSpeaker.Themes;
using OpenSpeaker.ViewModels;
namespace OpenSpeaker;

public partial class App : Application
{
    private AppBootstrapper? _boot;
    private MainWindow? _window;
    private ProfileService? _profileService;
    private ProfileViewModel? _profileVm;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, ex) =>
        {
            var msg = $"Unhandled exception:\n\n{ex.Exception.GetType().Name}: {ex.Exception.Message}\n\n{ex.Exception.StackTrace}";
            _boot?.Logger?.Error("UNHANDLED", ex.Exception);
            System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), $"[{DateTime.Now:O}] {msg}\n\n");
            MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            var e2 = ex.ExceptionObject as Exception;
            var msg = $"Fatal exception:\n\n{e2?.GetType().Name}: {e2?.Message}\n\n{e2?.StackTrace}";
            _boot?.Logger?.Error("FATAL", e2);
            System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), $"[{DateTime.Now:O}] {msg}\n\n");
            MessageBox.Show(msg, "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        EventManager.RegisterClassHandler(
            typeof(FrameworkElement),
            ToolTipService.ToolTipOpeningEvent,
            new ToolTipEventHandler(OnGlobalToolTipOpening));

        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            _profileService = new ProfileService(appDir);
            var manifest = _profileService.Load();

            _profileVm = new ProfileViewModel(_profileService);
            _profileVm.OnSwitch = name => _ = SwitchProfileAsync(name);

            _boot = new AppBootstrapper(_profileService.GetDbPath(manifest.ActiveProfile));

            var settings = _boot.SettingsRepo.GetSettings();
            ThemeService.Apply(settings.Theme);
            LocalizationService.Load(settings.Language);
            UiController.Instance.ShowTooltips = settings.ShowTooltips;

            await _boot.StartAsync();

            var viewModel = new MainWindowViewModel(_boot, _profileVm);
            _window = new MainWindow(_boot, viewModel);
            _window.Show();
            _window.Activate();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"OpenSpeaker failed to start:\n\n{ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private async Task SwitchProfileAsync(string name)
    {
        if (_boot == null || _window == null || _profileService == null || _profileVm == null) return;

        _profileService.SetActive(name);
        var dbPath = _profileService.GetDbPath(name);

        await _boot.StopAsync();
        _boot.Dispose();

        _boot = new AppBootstrapper(dbPath);

        var settings = _boot.SettingsRepo.GetSettings();
        ThemeService.Apply(settings.Theme);

        await _boot.StartAsync();

        (_window.DataContext as IDisposable)?.Dispose();

        var viewModel = new MainWindowViewModel(_boot, _profileVm);
        _window.SetBootstrapper(_boot);
        _window.DataContext = viewModel;
    }

    private static void OnGlobalToolTipOpening(object sender, ToolTipEventArgs e)
    {
        if (!UiController.Instance.ShowTooltips)
            e.Handled = true;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_boot != null)
            await _boot.StopAsync();
        base.OnExit(e);
    }
}
