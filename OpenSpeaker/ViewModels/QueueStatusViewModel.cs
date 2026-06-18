using OpenSpeaker.Queue;
namespace OpenSpeaker.ViewModels;

public class QueueStatusViewModel : BaseViewModel
{
    private readonly ITtsQueue _queue;
    private int _count;
    private bool _isPaused;

    public int Count { get => _count; private set => SetField(ref _count, value); }
    public bool IsPaused { get => _isPaused; private set => SetField(ref _isPaused, value); }

    public RelayCommand PauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand StopCommand { get; }

    public QueueStatusViewModel(ITtsQueue queue)
    {
        _queue = queue;
        PauseCommand = new RelayCommand(() => _queue.Pause());
        ResumeCommand = new RelayCommand(() => _queue.Resume());
        ClearCommand = new RelayCommand(() => _queue.Clear());
        StopCommand = new RelayCommand(() => _queue.Stop());

        _queue.ItemStarted += (_, _) => UpdateStatus();
        _queue.ItemCompleted += (_, _) => UpdateStatus();
    }

    private void UpdateStatus()
    {
        Count = _queue.Count;
        IsPaused = _queue.IsPaused;
    }
}
