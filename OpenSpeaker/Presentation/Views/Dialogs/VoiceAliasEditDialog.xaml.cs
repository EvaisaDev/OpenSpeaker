using System.Windows;
using OpenSpeaker.ViewModels;
namespace OpenSpeaker.Views.Dialogs;

public partial class VoiceAliasEditDialog : Window
{
    public VoiceAliasEditDialog(VoiceAliasEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
