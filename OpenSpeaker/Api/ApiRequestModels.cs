using Newtonsoft.Json;
namespace OpenSpeaker.Api;

public class BaseRequest
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("request")]
    public string Request { get; set; } = string.Empty;
}

public class SpeakRequest : BaseRequest
{
    [JsonProperty("voice")]
    public string Voice { get; set; } = string.Empty;

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("badWordFilter")]
    public bool BadWordFilter { get; set; } = true;

    [JsonProperty("silent")]
    public bool Silent { get; set; } = false;

    [JsonProperty("delay")]
    public bool Delay { get; set; } = false;
}

public class ModeRequest : BaseRequest
{
    [JsonProperty("mode")]
    public string Mode { get; set; } = string.Empty;
}

public class EventsRequest : BaseRequest
{
    [JsonProperty("state")]
    public string State { get; set; } = string.Empty;
}

public class UdpBaseRequest
{
    [JsonProperty("command")]
    public string Command { get; set; } = string.Empty;

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
}

public class UdpSpeakRequest : UdpBaseRequest
{
    [JsonProperty("voice")]
    public string Voice { get; set; } = string.Empty;

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
}

public class UdpEventsRequest : UdpBaseRequest
{
    [JsonProperty("value")]
    public string Value { get; set; } = string.Empty;

    [JsonProperty("state")]
    public string State { get; set; } = string.Empty;
}

public class UdpRegRequest : UdpBaseRequest
{
    [JsonProperty("mode")]
    public string Mode { get; set; } = string.Empty;

    [JsonProperty("user")]
    public string User { get; set; } = string.Empty;

    [JsonProperty("id")]
    public new string Id { get; set; } = string.Empty;
}

public class UdpSetRequest : UdpBaseRequest
{
    [JsonProperty("method")]
    public string Method { get; set; } = string.Empty;

    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;

    [JsonProperty("nickname")]
    public string Nickname { get; set; } = string.Empty;

    [JsonProperty("value")]
    public object? Value { get; set; }
}

public class UdpAssignRequest : UdpBaseRequest
{
    [JsonProperty("method")]
    public string Method { get; set; } = string.Empty;

    [JsonProperty("username")]
    public string Username { get; set; } = string.Empty;
}

public class UdpProfileRequest : UdpBaseRequest
{
    [JsonProperty("profile")]
    public string Profile { get; set; } = string.Empty;
}
