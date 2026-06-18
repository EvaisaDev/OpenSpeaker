using OpenSpeaker.Import;
namespace OpenSpeaker.ViewModels;

public class ImportViewModel : BaseViewModel
{
    private readonly SpeakerBotImporter _importer;

    private string _folderPath = string.Empty;
    public string FolderPath { get => _folderPath; set => SetField(ref _folderPath, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set { SetField(ref _statusMessage, value); OnPropertyChanged(nameof(HasStatusMessage)); }
    }
    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

    private bool _isError;
    public bool IsError { get => _isError; set => SetField(ref _isError, value); }

    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand ImportCommand { get; }

    public ImportViewModel(SpeakerBotImporter importer)
    {
        _importer = importer;
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        ImportCommand = new RelayCommand(RunImport, () => !string.IsNullOrWhiteSpace(FolderPath));
    }

    private void BrowseFolder()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select your Speaker.bot install folder"
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            FolderPath = dialog.SelectedPath;
    }

    private void RunImport()
    {
        StatusMessage = string.Empty;
        IsError = false;
        try
        {
            var result = _importer.Import(FolderPath);

            var authStr = result.AuthImported ? "yes" : "no";
            var lines = new List<string>
            {
                "Import complete — restart to apply all changes.",
                $"  Users: {result.UsersImported}  |  Aliases: {result.AliasesImported}  |  Events: {result.EventsImported}  |  Rewards: {result.RewardsImported}  |  Engines: {result.EnginesImported}  |  Auth: {authStr}",
            };
            if (result.Warnings.Count > 0)
                lines.AddRange(result.Warnings.Select(w => $"  Warning: {w}"));

            StatusMessage = string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex)
        {
            IsError = true;
            StatusMessage = $"Import failed: {ex.Message}";
        }
    }
}
