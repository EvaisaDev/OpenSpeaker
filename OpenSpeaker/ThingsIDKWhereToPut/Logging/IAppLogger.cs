namespace OpenSpeaker.ThingsIDKWhereToPut.Logging;
public interface IAppLogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
    event EventHandler<LogEventArgs> LogMessage;
}

public class LogEventArgs : EventArgs
{
    public string Level { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
