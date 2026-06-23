using OpenSpeaker.Twitch.TwitchEventArgs;
namespace OpenSpeaker.Events;

public class VariableBuilder
{
    public Dictionary<string, string> FromFollow(FollowEventArgs e) => new()
    {
        ["name"] = e.Username
    };

    public Dictionary<string, string> FromSub(SubEventArgs e) => new()
    {
        ["name"] = e.Username,
        ["subtier"] = TierName(e.Tier),
        ["tier"] = e.Tier
    };

    public Dictionary<string, string> FromResub(ResubEventArgs e) => new()
    {
        ["name"] = e.Username,
        ["subtier"] = TierName(e.Tier),
        ["tier"] = e.Tier,
        ["cumulative"] = e.CumulativeMonths.ToString(),
        ["message"] = e.Message
    };

    public Dictionary<string, string> FromGiftSub(GiftSubEventArgs e) => new()
    {
        ["name"] = e.GiverUsername,
        ["recipient"] = e.RecipientUsername,
        ["subtier"] = TierName(e.Tier),
        ["tier"] = e.Tier
    };

    public Dictionary<string, string> FromGiftBomb(GiftBombEventArgs e) => new()
    {
        ["name"] = e.GiverUsername,
        ["gift"] = e.GiftCount.ToString(),
        ["subtier"] = TierName(e.Tier),
        ["tier"] = e.Tier
    };

    public Dictionary<string, string> FromCheer(CheerEventArgs e) => new()
    {
        ["name"] = e.Username,
        ["bits"] = e.Bits.ToString(),
        ["message"] = e.Message
    };

    public Dictionary<string, string> FromRaid(RaidEventArgs e) => new()
    {
        ["name"] = e.FromUsername,
        ["amount"] = e.ViewerCount.ToString()
    };

    public Dictionary<string, string> FromChannelPoint(ChannelPointEventArgs e) => new()
    {
        ["name"] = e.Username,
        ["title"] = e.RewardTitle,
        ["cost"] = e.RewardCost.ToString(),
        ["input"] = e.Input
    };

    public Dictionary<string, string> FromHypeTrain(HypeTrainEventArgs e) => new()
    {
        ["level"] = e.Level.ToString(),
        ["progress"] = e.Progress.ToString(),
        ["percent"] = e.Percent.ToString()
    };

    public Dictionary<string, string> FromGoal(GoalEventArgs e) => new()
    {
        ["amount"] = e.CurrentAmount.ToString(),
        ["target"] = e.TargetAmount.ToString()
    };

    public Dictionary<string, string> FromChat(string username, string displayName, string message) => new()
    {
        ["name"] = username,
        ["displayname"] = displayName,
        ["message"] = message
    };

    private static string TierName(string tier) => tier switch
    {
        "1000" => "Tier 1",
        "2000" => "Tier 2",
        "3000" => "Tier 3",
        _ => tier
    };
}
