using System.Windows;
using OpenSpeaker.ViewModels;
namespace OpenSpeaker.Views;

public partial class SyncWindow : Window
{
    public SyncWindow(SyncViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose = () => Dispatcher.Invoke(Close);
        Loaded += async (_, _) => await viewModel.RefreshAsync();
        Closing += (_, e) => { if (viewModel.IsBusy) e.Cancel = true; };
    }
}
