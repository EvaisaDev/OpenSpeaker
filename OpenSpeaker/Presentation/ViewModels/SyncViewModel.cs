using System.Collections.ObjectModel;
using System.Windows;
using OpenSpeaker.Sync;
namespace OpenSpeaker.ViewModels;

public class SyncViewModel : BaseViewModel
{
    private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(3);

    private readonly NetworkSyncService _sync;
    private readonly IDialogService _dialogs;

    public Func<string, SyncInstanceInfo, Func<string, bool>, Task>? OnApply { get; set; }
    public Action? RequestClose { get; set; }

    public ObservableCollection<SyncInstanceInfo> Instances { get; } = new();
    public ObservableCollection<SyncCategoryOption> Categories { get; } = new();

    private SyncInstanceInfo? _selectedInstance;
    public SyncInstanceInfo? SelectedInstance
    {
        get => _selectedInstance;
        set { SetField(ref _selectedInstance, value); OnPropertyChanged(nameof(HasSelection)); }
    }

    public bool HasSelection => SelectedInstance != null;
    public bool HasCategorySelection => Categories.Any(c => c.IsSelected);

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set { SetField(ref _isBusy, value); OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsIdle => !IsBusy;

    private string _statusText = string.Empty;
    public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand SyncCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }

    public SyncViewModel(NetworkSyncService sync, IDialogService? dialogs = null)
    {
        _sync = sync;
        _dialogs = dialogs ?? new DialogService();
        foreach (var cat in SyncCategory.All)
            Categories.Add(new SyncCategoryOption(cat, Loc(cat.LabelKey)));
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        SyncCommand = new AsyncRelayCommand(PullAsync, () => !IsBusy && HasSelection && HasCategorySelection);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(), () => !IsBusy);
        SelectAllCommand = new RelayCommand(() => SetAllCategories(true), () => !IsBusy);
        SelectNoneCommand = new RelayCommand(() => SetAllCategories(false), () => !IsBusy);
    }

    private void SetAllCategories(bool selected)
    {
        foreach (var c in Categories) c.IsSelected = selected;
    }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        StatusText = Loc("Sync.Searching");
        try
        {
            var found = await _sync.DiscoverAsync(DiscoveryTimeout);
            var previous = SelectedInstance?.SessionToken;
            Instances.Clear();
            foreach (var i in found) Instances.Add(i);
            SelectedInstance = Instances.FirstOrDefault(i => i.SessionToken == previous);
            StatusText = Instances.Count == 0
                ? Loc("Sync.NoneFound")
                : string.Format(Loc("Sync.FoundCount"), Instances.Count);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PullAsync()
    {
        var target = SelectedInstance;
        if (target == null || OnApply == null) return;

        var selectedKeys = Categories.Where(c => c.IsSelected).Select(c => c.Category.Key).ToList();
        if (selectedKeys.Count == 0) return;
        var filter = SyncCategory.BuildFilter(selectedKeys);
        var selectedLabels = string.Join(", ", Categories.Where(c => c.IsSelected).Select(c => c.Label));

        var confirm = string.Format(Loc("Sync.ConfirmMessage"), target.InstanceName, target.Endpoint, selectedLabels);
        if (!_dialogs.Confirm(confirm, Loc("Sync.Title"))) return;

        IsBusy = true;
        StatusText = string.Format(Loc("Sync.Pulling"), target.InstanceName);
        var succeeded = false;
        try
        {
            var progress = new Progress<long>(bytes => StatusText = string.Format(Loc("Sync.Receiving"), bytes / 1024));
            var json = await _sync.PullAsync(target, progress);
            StatusText = Loc("Sync.Applying");
            await OnApply(json, target, filter);
            succeeded = true;
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            _dialogs.ShowError(string.Format(Loc("Sync.Failed"), ex.Message), Loc("Sync.Title"));
        }
        finally
        {
            IsBusy = false;
        }

        if (!succeeded) return;
        RequestClose?.Invoke();
        _dialogs.ShowInfo(string.Format(Loc("Sync.Done"), target.InstanceName), Loc("Sync.Title"));
    }

    private static string Loc(string key) =>
        Application.Current?.TryFindResource(key) as string ?? key;
}

public class SyncCategoryOption : BaseViewModel
{
    public SyncCategory Category { get; }
    public string Label { get; }

    private bool _isSelected = true;
    public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

    public SyncCategoryOption(SyncCategory category, string label)
    {
        Category = category;
        Label = label;
    }
}
