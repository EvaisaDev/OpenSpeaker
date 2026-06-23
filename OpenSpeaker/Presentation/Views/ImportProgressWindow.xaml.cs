using System.Windows;
using OpenSpeaker.ViewModels;
namespace OpenSpeaker.Views;

public partial class ImportProgressWindow : Window
{
    public ImportProgressWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is ImportProgressViewModel vm)
                vm.LogLines.CollectionChanged += (_, _) =>
                    Dispatcher.BeginInvoke(() => LogScroll.ScrollToBottom());
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
