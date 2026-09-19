namespace OpenSpeaker.Core;
public static class Constants
{
    public const int WebSocketPort = 7580;
    public const int EventsWebSocketPort = 7581;
    public const int UdpPort = 6669;
    public const int SyncDiscoveryPort = 7583;
    public const string SyncMulticastAddress = "239.255.75.80";
    public const string WebSocketEndpoint = "/";
    public const string WebSocketAddress = "127.0.0.1";
    public const string DatabaseFileName = "openspeaker.db";
    public const string DefaultVoiceAlias = "Default";
    public const int SpeakTimeoutSeconds = 30;
}
