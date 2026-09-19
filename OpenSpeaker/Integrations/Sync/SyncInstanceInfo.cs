namespace OpenSpeaker.Sync;

public record SyncInstanceInfo(string SessionToken, string InstanceId, string InstanceName, string Version, string Host, int Port)
{
    public string Endpoint => $"{Host}:{Port}";
}
