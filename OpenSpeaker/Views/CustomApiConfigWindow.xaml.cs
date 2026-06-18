using System.Windows;
using OpenSpeaker.ViewModels;
namespace OpenSpeaker.Views;

public partial class CustomApiConfigWindow : Window
{
    public CustomApiConfigWindow(CustomApiEditViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
