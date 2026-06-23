using System.IO;
using Serilog;
using Serilog.Events;
namespace OpenSpeaker.ThingsIDKWhereToPut.Logging;

public class AppLogger : IAppLogger
{
    private readonly Serilog.ILogger _logger;
    private readonly string _minLevel;

    public event EventHandler<LogEventArgs>? LogMessage;

    public AppLogger(string logDirectory, string minLevel = "Info")
    {
        _minLevel = minLevel;
        var level = minLevel.ToLower() switch
        {
            "verbose" or "debug" => LogEventLevel.Debug,
            "warn" or "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            _ => LogEventLevel.Information
        };

        _logger = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .WriteTo.File(
                Path.Combine(logDirectory, "openspeaker-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
    }

    public void Debug(string message)
    {
        _logger.Debug(message);
        Raise("Debug", message);
    }

    public void Info(string message)
    {
        _logger.Information(message);
        Raise("Info", message);
    }

    public void Warn(string message)
    {
        _logger.Warning(message);
        Raise("Warn", message);
    }

    public void Error(string message, Exception? ex = null)
    {
        if (ex != null)
            _logger.Error(ex, message);
        else
            _logger.Error(message);
        Raise("Error", message);
    }

    private void Raise(string level, string message)
    {
        LogMessage?.Invoke(this, new LogEventArgs { Level = level, Message = message });
    }
}
