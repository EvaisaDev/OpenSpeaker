using OpenSpeaker.Data;
using OpenSpeaker.Models;
using OpenSpeaker.Queue;
using OpenSpeaker.Users;
namespace OpenSpeaker.Chat;

public class CustomCommandHandler : IChatCommandHandler
{
    private readonly CustomCommandRepository _repo;
    private readonly PermissionChecker _permissionChecker;
    private readonly ITtsQueue _queue;

    public CustomCommandHandler(
        CustomCommandRepository repo,
        PermissionChecker permissionChecker,
        ITtsQueue queue)
    {
        _repo = repo;
        _permissionChecker = permissionChecker;
        _queue = queue;
    }

    public Task<bool> HandleAsync(string twitchId, string username, List<string> roles, string rawMessage)
    {
        var commands = _repo.GetAll().Where(c => c.Enabled);
        foreach (var cmd in commands)
        {
            if (!rawMessage.StartsWith(cmd.Trigger, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!cmd.AllowedRoles.Any(r => roles.Contains(r)) && !roles.Contains(UserRoles.Broadcaster))
                return Task.FromResult(false);

            var text = rawMessage.Substring(cmd.Trigger.Length).Trim();
            if (string.IsNullOrEmpty(text))
                return Task.FromResult(true);

            _queue.Enqueue(new TtsQueueItem
            {
                Text = text,
                VoiceAliasName = cmd.VoiceAliasName,
                UserId = twitchId
            });

            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
