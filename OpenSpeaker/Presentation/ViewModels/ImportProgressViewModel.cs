using System.Collections.ObjectModel;
namespace OpenSpeaker.ViewModels;

public class ImportProgressViewModel : BaseViewModel
{
    public ObservableCollection<string> LogLines { get; } = new();

    private int _progressValue;
    public int ProgressValue { get => _progressValue; set => SetField(ref _progressValue, value); }

    private bool _isDone;
    public bool IsDone { get => _isDone; set => SetField(ref _isDone, value); }

    private string _statusText = "Starting import...";
    public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }

    public void AdvanceStage(string message)
    {
        LogLines.Add(message);
        StatusText = message;
        ProgressValue++;
    }

    public void AddDetail(string message) => LogLines.Add($"  {message}");

    public void AddWarning(string warning) => LogLines.Add($"  Warning: {warning}");

    public void SetComplete(string summary)
    {
        LogLines.Add(summary);
        LogLines.Add("Restart to apply all changes.");
        StatusText = "Import complete!";
        ProgressValue = 9;
        IsDone = true;
    }

    public void SetError(string message)
    {
        LogLines.Add($"Error: {message}");
        StatusText = "Import failed.";
        IsDone = true;
    }
}
