using System.Windows;
using OpenSpeaker.Extensions;
using OpenSpeaker.Import;
using OpenSpeaker.Views;
namespace OpenSpeaker.ViewModels;

public class ImportViewModel : BaseViewModel
{
    private readonly SpeakerBotImporter _importer;
    private readonly ExtensionManager _extensions;
    private readonly IDialogService _dialogs;
    public Action? OnComplete { get; set; }

    private string _folderPath = string.Empty;
    public string FolderPath { get => _folderPath; set => SetField(ref _folderPath, value); }

    public RelayCommand BrowseFolderCommand { get; }
    public AsyncRelayCommand ImportCommand { get; }

    public ImportViewModel(SpeakerBotImporter importer, ExtensionManager extensions, IDialogService? dialogs = null)
    {
        _importer = importer;
        _extensions = extensions;
        _dialogs = dialogs ?? new DialogService();
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        ImportCommand = new AsyncRelayCommand(RunImport, () => !string.IsNullOrWhiteSpace(FolderPath));
    }

    private void BrowseFolder()
    {
        var path = _dialogs.PickFolder("Select your Speaker.bot install folder");
        if (path != null) FolderPath = path;
    }

    private async Task RunImport()
    {
        var vm = new ImportProgressViewModel();
        var window = new ImportProgressWindow { DataContext = vm, Owner = Application.Current.MainWindow };

        var stageProgress = new Progress<string>(vm.AdvanceStage);
        var detailProgress = new Progress<string>(vm.AddDetail);
        window.Show();

        try
        {
            var migration = _extensions.CollectMigrationData();
            var result = await Task.Run(() => _importer.Import(FolderPath, stageProgress, detailProgress, migration));

            foreach (var w in result.Warnings)
                vm.AddWarning(w);

            var authStr = result.AuthImported ? "yes" : "no";
            vm.SetComplete($"Users: {result.UsersImported}  |  Aliases: {result.AliasesImported}  |  Events: {result.EventsImported}  |  Rewards: {result.RewardsImported}  |  Engines: {result.EnginesImported}  |  Auth: {authStr}");
            OnComplete?.Invoke();
        }
        catch (Exception ex)
        {
            vm.SetError(ex.Message);
        }
    }
}
