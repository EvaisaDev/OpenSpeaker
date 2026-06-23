using System.Windows;
namespace OpenSpeaker.Views.Dialogs;

public partial class CreateProfileDialog : Window
{
    public string ProfileName { get; private set; } = string.Empty;

    public CreateProfileDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    private void Create_Click(object sender, RoutedEventArgs e) => Confirm();
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    private void NameBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Return) Confirm(); }

    private void Confirm()
    {
        var name = NameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        ProfileName = name;
        DialogResult = true;
        Close();
    }
}
