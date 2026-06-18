using System.Collections.Generic;
namespace OpenSpeaker.Infrastructure.Extensions;
public static class DisposableExtensions
{
    public static T DisposeWith<T>(this T disposable, ICollection<IDisposable> collection) where T : IDisposable
    {
        collection.Add(disposable);
        return disposable;
    }
}
