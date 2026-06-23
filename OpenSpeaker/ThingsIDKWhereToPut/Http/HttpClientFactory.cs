using System.Net.Http;
namespace OpenSpeaker.ThingsIDKWhereToPut.Http;
public static class HttpClientFactory
{
    private static readonly Dictionary<string, HttpClient> _clients = new();
    private static readonly string UserAgent = "OpenSpeaker/1.0 (https://github.com/openspeaker)";

    public static HttpClient GetClient(string name)
    {
        if (_clients.TryGetValue(name, out var existing))
            return existing;

        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        _clients[name] = client;
        return client;
    }

    public static HttpClient GetClient(string name, string baseUrl)
    {
        var client = GetClient(name);
        if (client.BaseAddress == null)
            client.BaseAddress = new Uri(baseUrl);
        return client;
    }
}
