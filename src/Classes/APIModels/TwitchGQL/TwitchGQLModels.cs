namespace StreamTalkerClient.Classes.APIModels.TwitchGQL;

public class RewardsRoot
{
    public Data? data { get; set; }
    public Extensions? extensions { get; set; }
}

public class Data
{
    public Community? community { get; set; }
    public object? currentUser { get; set; }
}

public class Community
{
    public string? id { get; set; }
    public string? displayName { get; set; }
    public Channel? channel { get; set; }
    public string? __typename { get; set; }
    public object? self { get; set; }
}

public class Channel
{
    public string? id { get; set; }
    public Self? self { get; set; }
    public string? __typename { get; set; }
    public CommunityPointsSettings? communityPointsSettings { get; set; }
}

public class Self
{
    public object? communityPoints { get; set; }
    public string? __typename { get; set; }
}

public class CommunityPointsSettings
{
    public string? name { get; set; }
    public Image? image { get; set; }
    public string? __typename { get; set; }
    public AutomaticReward[]? automaticRewards { get; set; }
    public CustomReward[]? customRewards { get; set; }
    public List<object>? goals { get; set; }
    public bool isEnabled { get; set; }
    public int raidPointAmount { get; set; }
    public EmoteVariant[]? emoteVariants { get; set; }
    public Earning? earning { get; set; }
}

public class CustomReward
{
    public string? id { get; set; }
    public string? backgroundColor { get; set; }
    public object? cooldownExpiresAt { get; set; }
    public int cost { get; set; }
    public DefaultImage? defaultImage { get; set; }
    public Image? image { get; set; }
    public MaxPerStreamSetting? maxPerStreamSetting { get; set; }
    public MaxPerUserPerStreamSetting? maxPerUserPerStreamSetting { get; set; }
    public GlobalCooldownSetting? globalCooldownSetting { get; set; }
    public bool isEnabled { get; set; }
    public bool isInStock { get; set; }
    public bool isPaused { get; set; }
    public bool isSubOnly { get; set; }
    public bool isUserInputRequired { get; set; }
    public bool shouldRedemptionsSkipRequestQueue { get; set; }
    public object? redemptionsRedeemedCurrentStream { get; set; }
    public string? prompt { get; set; }
    public string? title { get; set; }
    public string? updatedForIndicatorAt { get; set; }
    public string? __typename { get; set; }
}

public class AutomaticReward
{
    public string? id { get; set; }
    public string? backgroundColor { get; set; }
    public int? cost { get; set; }
    public string? defaultBackgroundColor { get; set; }
    public int defaultCost { get; set; }
    public DefaultImage? defaultImage { get; set; }
    public object? image { get; set; }
    public bool isEnabled { get; set; }
    public bool isHiddenForSubs { get; set; }
    public int minimumCost { get; set; }
    public string? type { get; set; }
    public string? updatedForIndicatorAt { get; set; }
    public DateTime globallyUpdatedForIndicatorAt { get; set; }
    public string? __typename { get; set; }
}

public class DefaultImage
{
    public string? url { get; set; }
    public string? url2x { get; set; }
    public string? url4x { get; set; }
    public string? __typename { get; set; }
}

public class Image
{
    public string? url { get; set; }
    public string? url2x { get; set; }
    public string? url4x { get; set; }
    public string? __typename { get; set; }
}

public class MaxPerStreamSetting
{
    public bool isEnabled { get; set; }
    public int maxPerStream { get; set; }
    public string? __typename { get; set; }
}

public class MaxPerUserPerStreamSetting
{
    public bool isEnabled { get; set; }
    public int maxPerUserPerStream { get; set; }
    public string? __typename { get; set; }
}

public class GlobalCooldownSetting
{
    public bool isEnabled { get; set; }
    public int globalCooldownSeconds { get; set; }
    public string? __typename { get; set; }
}

public class Extensions
{
    public int durationMilliseconds { get; set; }
    public string? operationName { get; set; }
    public string? requestID { get; set; }
}

public class EmoteVariant
{
    public string? id { get; set; }
    public bool isUnlockable { get; set; }
    public Emote? emote { get; set; }
    public List<Modification>? modifications { get; set; }
    public string? __typename { get; set; }
}

public class Emote
{
    public string? id { get; set; }
    public string? token { get; set; }
    public string? __typename { get; set; }
}

public class Modification
{
    public string? id { get; set; }
    public Emote? emote { get; set; }
    public Modifier? modifier { get; set; }
    public DateTime globallyUpdatedForIndicatorAt { get; set; }
    public string? __typename { get; set; }
}

public class Modifier
{
    public string? id { get; set; }
    public string? __typename { get; set; }
}

public class Earning
{
    public string? id { get; set; }
    public int averagePointsPerHour { get; set; }
    public int cheerPoints { get; set; }
    public int claimPoints { get; set; }
    public int followPoints { get; set; }
    public int passiveWatchPoints { get; set; }
    public int raidPoints { get; set; }
    public int subscriptionGiftPoints { get; set; }
    public List<WatchStreakPoint>? watchStreakPoints { get; set; }
    public List<Multiplier>? multipliers { get; set; }
    public string? __typename { get; set; }
}

public class WatchStreakPoint
{
    public int points { get; set; }
    public string? __typename { get; set; }
}

public class Multiplier
{
    public string? reasonCode { get; set; }
    public double factor { get; set; }
    public string? __typename { get; set; }
}
