namespace OpenSpeaker.Infrastructure.Logging;

public static class TaskExtensions
{
    public static void Forget(this Task task, IAppLogger? logger, string context)
    {
        task.ContinueWith(
            t => logger?.Error($"{context} failed: {t.Exception?.GetBaseException().Message}"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
