using System.Windows;
using System.Windows.Threading;
using OpenSpeaker.ViewModels;
namespace OpenSpeaker.Views;

public partial class GenericSpeakerWindow : Window
{
    public GenericSpeakerWindow(GenericSpeakerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.VoicesLoaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Background, Prewarm);
    }

    private void Prewarm()
    {
        VoiceComboBox.IsDropDownOpen = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => VoiceComboBox.IsDropDownOpen = false);
    }
}
