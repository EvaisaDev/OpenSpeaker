using System.Windows;
using OpenSpeaker.ViewModels;
namespace OpenSpeaker.Views;

public partial class ExtensionAuthWindow : Window
{
    public ExtensionAuthWindow(ExtensionAuthViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
