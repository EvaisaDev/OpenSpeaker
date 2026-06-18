using System.Windows;
using System.Windows.Controls;
using OpenSpeaker.Core;
using OpenSpeaker.Localization;
using OpenSpeaker.ViewModels;
namespace OpenSpeaker;

public partial class App : Application
{
    private AppBootstrapper? _boot;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        EventManager.RegisterClassHandler(
            typeof(FrameworkElement),
            ToolTipService.ToolTipOpeningEvent,
            new ToolTipEventHandler(OnGlobalToolTipOpening));

        try
        {
            _boot = new AppBootstrapper();
            ServiceLocator.Initialize(_boot);

            var settings = _boot.SettingsRepo.GetSettings();
            LocalizationService.Load(settings.Language);
            UiController.Instance.ShowTooltips = settings.ShowTooltips;

            await _boot.StartAsync();

            var viewModel = new MainWindowViewModel(_boot);
            var window = new MainWindow(_boot, viewModel);
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"OpenSpeaker failed to start:\n\n{ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
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
