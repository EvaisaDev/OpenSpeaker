using System.Windows;
using OpenSpeaker.ViewModels;
namespace OpenSpeaker.Views;

public partial class GenericSpeakerWindow : Window
{
    public GenericSpeakerWindow(GenericSpeakerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
