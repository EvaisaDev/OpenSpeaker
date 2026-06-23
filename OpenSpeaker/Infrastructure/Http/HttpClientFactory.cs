using System.Net.Http;
using System.Collections.Concurrent;
namespace OpenSpeaker.Infrastructure.Http;
public static class HttpClientFactory
{
    private static readonly ConcurrentDictionary<string, Lazy<HttpClient>> _clients = new();
    private static readonly string UserAgent = "OpenSpeaker/1.0 (https://github.com/openspeaker)";

    public static HttpClient GetClient(string name) => GetClient(name, null);

    public static HttpClient GetClient(string name, string? baseUrl)
    {
        return _clients.GetOrAdd(name, _ => new Lazy<HttpClient>(() =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            if (!string.IsNullOrEmpty(baseUrl))
                client.BaseAddress = new Uri(baseUrl);
            return client;
        })).Value;
    }
}
