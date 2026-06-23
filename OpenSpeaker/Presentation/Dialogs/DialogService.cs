namespace OpenSpeaker.ViewModels;

public interface IDialogService
{
    string? PickFolder(string? description = null);
    string? PickFile(string filter, string? title = null);
    void ShowInfo(string message, string title);
    void ShowWarning(string message, string title);
    void ShowError(string message, string title);
    bool Confirm(string message, string title);
}

public class DialogService : IDialogService
{
    public string? PickFolder(string? description = null)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (!string.IsNullOrEmpty(description)) dialog.Description = description;
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }

    public string? PickFile(string filter, string? title = null)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog { Filter = filter };
        if (!string.IsNullOrEmpty(title)) dialog.Title = title;
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.FileName : null;
    }

    public void ShowInfo(string message, string title) =>
        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

    public void ShowWarning(string message, string title) =>
        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);

    public void ShowError(string message, string title) =>
        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

    public bool Confirm(string message, string title) =>
        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.Yes;
}
