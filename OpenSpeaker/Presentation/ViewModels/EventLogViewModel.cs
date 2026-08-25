using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using OpenSpeaker.Infrastructure.Logging;
namespace OpenSpeaker.ViewModels;

public class EventLogEntry
{
    public string Timestamp { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Color { get; init; } = "#C0C0D8";
}

public class EventLogViewModel : BaseViewModel
{
    public ObservableCollection<EventLogEntry> Entries { get; } = new();

    public RelayCommand ClearCommand { get; }


    private readonly ConcurrentQueue<EventLogEntry> _pending = new();
    private int _flushScheduled;

    public EventLogViewModel(IAppLogger logger)
    {
        ClearCommand = new RelayCommand(() => Entries.Clear());
        logger.LogMessage += (_, e) =>
        {
            var (category, color) = e.Level.ToUpper() switch
            {
                "INFO"  => ("INFO", "#9B8FFF"),
                "WARN"  => ("WARN", "#F59E0B"),
                "ERROR" => ("ERROR", "#EF4444"),
                "DEBUG" => ("DEBUG", "#6080A0"),
                _       => ("INFO", "#9B8FFF")
            };

            var parts = e.Message.Split("::", 2);
            var cat = parts.Length > 1 ? parts[0].Trim() : category;
            var msg = parts.Length > 1 ? parts[1].Trim() : e.Message;

            var catColor = cat.ToUpper() switch
            {
                "WEBSOCKET" => "#3B82F6",
                "TWITCH"    => "#A855F7",
                "UPDATE"    => "#A855F7",
                "TTS"       => "#C0C0D8",
                "UDP"       => "#22D3EE",
                _           => color
            };

            var entry = new EventLogEntry
            {
                Timestamp = e.Timestamp.ToString("[dd/MM/yyyy HH:mm:ss]"),
                Category  = cat,
                Message   = msg,
                Color     = catColor
            };

            _pending.Enqueue(entry);
            if (Interlocked.CompareExchange(ref _flushScheduled, 1, 0) == 0)
                Application.Current?.Dispatcher.BeginInvoke(new Action(FlushPending));
        };
    }

    private void FlushPending()
    {
        Interlocked.Exchange(ref _flushScheduled, 0);
        while (_pending.TryDequeue(out var entry))
        {
            Entries.Add(entry);
            if (Entries.Count > 500)
                Entries.RemoveAt(0);
        }
    }
}
