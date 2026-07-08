using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OpenSpeaker.Core;
using OpenSpeaker.Input;
using OpenSpeaker.ViewModels;
namespace OpenSpeaker;

public partial class MainWindow : Window
{
    private AppBootstrapper _boot;
    private NotifyIcon? _trayIcon;

    public MainWindow(AppBootstrapper boot, MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _boot = boot;
        DataContext = viewModel;

        var settings = boot.SettingsRepo.GetSettings();
        Left = settings.WindowLeft;
        Top = settings.WindowTop;

        if (settings.MinimizeToTray)
            SetupTrayIcon();

        viewModel.VoiceAliases.VoicesLoaded += (_, _) =>
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                AliasVoiceComboBox.IsDropDownOpen = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => AliasVoiceComboBox.IsDropDownOpen = false);
            });
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Text = "OpenSpeaker",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = false
        };
        _trayIcon.DoubleClick += (_, _) =>
        {
            Show();
            WindowState = WindowState.Normal;
            _trayIcon.Visible = false;
        };

        var ctx = new ContextMenuStrip();
        ctx.Items.Add("Open", null, (_, _) =>
        {
            Show();
            WindowState = WindowState.Normal;
            _trayIcon.Visible = false;
        });
        ctx.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        });
        _trayIcon.ContextMenuStrip = ctx;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        var settings = _boot.SettingsRepo.GetSettings();
        if (settings.MinimizeToTray && WindowState == WindowState.Minimized && _trayIcon != null)
        {
            Hide();
            _trayIcon.Visible = true;
        }
    }

    internal void SetBootstrapper(AppBootstrapper boot) => _boot = boot;

    private void ProfileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        var selected = (string)e.AddedItems[0];
        var box = (System.Windows.Controls.ComboBox)sender;
        if (DataContext is not MainWindowViewModel vm) return;

        if (selected == ViewModels.ProfileViewModel.CreateSentinel)
        {
            box.SelectedItem = vm.Profile.SelectedProfile;
            var dialog = new Views.Dialogs.CreateProfileDialog { Owner = this };
            if (dialog.ShowDialog() != true) return;
            vm.Profile.CreateProfile(dialog.ProfileName);
        }
        else
        {
            vm.Profile.SelectedProfile = selected;
        }
    }

    private void AliasVoicesDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var grid = (DataGrid)sender;
        var hit = VisualTreeHelper.HitTest(grid, e.GetPosition(grid));
        if (hit == null) return;
        DependencyObject? el = hit.VisualHit;
        while (el != null && el != grid)
        {
            if (el is DataGridRow row)
            {
                if (row.IsSelected)
                {
                    grid.UnselectAll();
                    e.Handled = true;
                }
                return;
            }
            el = VisualTreeHelper.GetParent(el);
        }
        grid.UnselectAll();
    }

    private void FiltersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var vm = (MainWindowViewModel)DataContext;
        vm.Replacement.OnSelectionChanged(((DataGrid)sender).SelectedItems);
    }

    private void FiltersDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var grid = (DataGrid)sender;
        var hit = VisualTreeHelper.HitTest(grid, e.GetPosition(grid));
        if (hit == null) { grid.UnselectAll(); return; }
        DependencyObject? el = hit.VisualHit;
        while (el != null && el != grid)
        {
            if (el is DataGridRow) return;
            el = VisualTreeHelper.GetParent(el);
        }
        grid.UnselectAll();
    }

    private void CommandsDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var grid = (DataGrid)sender;
        var hit = VisualTreeHelper.HitTest(grid, e.GetPosition(grid));
        if (hit == null) return;
        DependencyObject? el = hit.VisualHit;
        while (el != null && el != grid)
        {
            if (el is DataGridRow) return;
            el = VisualTreeHelper.GetParent(el);
        }
        grid.UnselectAll();
    }

    private void AliasItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item) item.IsSelected = true;
    }

    private readonly List<Key> _keybindCapture = new();

    private void KeybindButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { DataContext: SettingFieldViewModel vm } button)
        {
            _keybindCapture.Clear();
            vm.CaptureText = string.Empty;
            vm.IsCapturing = true;
            button.Focus();
        }
    }

    private void KeybindButton_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { DataContext: SettingFieldViewModel vm }) return;
        if (!vm.IsCapturing) return;

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape) { EndCapture(vm); return; }
        if (_keybindCapture.Count == 0 && key is Key.Back or Key.Delete)
        {
            vm.Value = string.Empty;
            EndCapture(vm);
            return;
        }

        if (!_keybindCapture.Contains(key)) _keybindCapture.Add(key);
        vm.CaptureText = FormatCapture();
    }

    private void KeybindButton_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { DataContext: SettingFieldViewModel vm }) return;
        if (!vm.IsCapturing || _keybindCapture.Count == 0) return;

        e.Handled = true;
        if (_keybindCapture.Any(Keyboard.IsKeyDown)) return;

        vm.Value = FormatCapture();
        EndCapture(vm);
    }

    private void KeybindButton_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { DataContext: SettingFieldViewModel vm })
            EndCapture(vm);
    }

    private string FormatCapture() => string.Join("+", _keybindCapture.Select(KeyNameMap.Display).Distinct());

    private void EndCapture(SettingFieldViewModel vm)
    {
        _keybindCapture.Clear();
        vm.CaptureText = string.Empty;
        vm.IsCapturing = false;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        var settings = _boot.SettingsRepo.GetSettings();

        if (settings.ConfirmationOnClose)
        {
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to close OpenSpeaker?",
                "Confirm Close",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        settings.WindowLeft = Left;
        settings.WindowTop = Top;
        _boot.SettingsRepo.SaveSettings(settings);

        _trayIcon?.Dispose();
        _ = _boot.StopAsync();
        _boot.Dispose();
    }
}
