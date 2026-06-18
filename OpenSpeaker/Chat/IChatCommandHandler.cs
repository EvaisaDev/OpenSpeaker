namespace OpenSpeaker.Chat;
public interface IChatCommandHandler
{
    Task<bool> HandleAsync(string twitchId, string username, List<string> roles, string rawMessage);
}
