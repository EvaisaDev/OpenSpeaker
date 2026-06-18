using System.Collections.ObjectModel;
using System.Windows;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
namespace OpenSpeaker.ViewModels;

public class ProcessingItemViewModel
{
    public string Display { get; init; } = string.Empty;
}

public class ProcessingViewModel : BaseViewModel
{
    public ObservableCollection<ProcessingItemViewModel> QueueItems { get; } = new();
    public ObservableCollection<ProcessingItemViewModel> ProcessedItems { get; } = new();

    private ProcessingItemViewModel? _selectedProcessed;
    public ProcessingItemViewModel? SelectedProcessed
    {
        get => _selectedProcessed;
        set => SetField(ref _selectedProcessed, value);
    }

    public ProcessingViewModel(ITtsQueue queue)
    {
        queue.ItemStarted += (_, e) =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var vm = BuildDisplay(e.Item);
                QueueItems.Add(vm);
            });
        };

        queue.ItemCompleted += (_, e) =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var match = QueueItems.FirstOrDefault(i => i.Display.Contains(e.Item.Text));
                if (match != null) QueueItems.Remove(match);

                var completed = BuildDisplay(e.Item);
                ProcessedItems.Insert(0, completed);
                if (ProcessedItems.Count > 200)
                    ProcessedItems.RemoveAt(ProcessedItems.Count - 1);
            });
        };
    }

    private static ProcessingItemViewModel BuildDisplay(TtsQueueItem item)
    {
        var display = $"{item.VoiceAliasName} :: {item.Username} :: {item.Text}";
        return new ProcessingItemViewModel { Display = display };
    }
}
