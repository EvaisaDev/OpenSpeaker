using System.Windows;
using OpenSpeaker.ViewModels;
namespace OpenSpeaker.Views;

public partial class EngineConfigWindow : Window
{
    public EngineConfigWindow(EngineConfigViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
