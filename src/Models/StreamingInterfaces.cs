using TwitchLib.Client.Models;
using StreamTalkerClient.Classes.APIModels.TwitchGQL;

namespace StreamTalkerClient.Models;

/// <summary>
/// Common interface for chat messages from any streaming platform.
/// </summary>
public interface IStreamMessage
{
    /// <summary>The username of the message sender.</summary>
    string Username { get; }

    /// <summary>The text content of the message.</summary>
    string Text { get; }

    /// <summary>Whether this message is associated with a reward/channel points redemption.</summary>
    bool IsReward { get; }

    /// <summary>The reward ID if this is a reward message, null otherwise.</summary>
    string? RewardId { get; }
}

/// <summary>
/// Common interface for channel rewards/channel points.
/// </summary>
public interface IStreamReward
{
    /// <summary>Unique identifier for the reward.</summary>
    string Id { get; }

    /// <summary>Display title of the reward.</summary>
    string Title { get; }
}

/// <summary>
/// Adapter for Twitch chat messages.
/// </summary>
public class TwitchMessageAdapter : IStreamMessage
{
    private readonly ChatMessage _message;
    private readonly string? _rewardId;

    public TwitchMessageAdapter(ChatMessage message, string? rewardId = null)
    {
        _message = message ?? throw new ArgumentNullException(nameof(message));
        _rewardId = rewardId;
    }

    public string Username => _message.Username;
    public string Text => _message.Message;
    public bool IsReward => !string.IsNullOrEmpty(_rewardId);
    public string? RewardId => _rewardId;

    /// <summary>Gets the underlying Twitch ChatMessage.</summary>
    public ChatMessage Original => _message;
}

/// <summary>
/// Adapter for VK Play chat messages.
/// </summary>
public class VKMessageAdapter : IStreamMessage
{
    private readonly VKChatMessage _message;

    public VKMessageAdapter(VKChatMessage message)
    {
        _message = message ?? throw new ArgumentNullException(nameof(message));
    }

    public string Username => _message.Author?.DisplayName ?? _message.Author?.Nick ?? "Unknown";
    public string Text => _message.GetText();
    public bool IsReward => _message.IsReward;
    public string? RewardId => _message.Reward?.Id;

    /// <summary>Gets the underlying VKChatMessage.</summary>
    public VKChatMessage Original => _message;
}

/// <summary>
/// Adapter for VK Play chat messages from ChatBot reward format.
/// Uses the actual author from the mention, not the ChatBot.
/// </summary>
public class VKMessageAdapterWithReward : IStreamMessage
{
    private readonly VKChatMessage _message;
    private readonly VKParsedRewardInfo _rewardInfo;
    private readonly string _rewardId;

    public VKMessageAdapterWithReward(VKChatMessage message, VKParsedRewardInfo rewardInfo, string rewardId)
    {
        _message = message ?? throw new ArgumentNullException(nameof(message));
        _rewardInfo = rewardInfo ?? throw new ArgumentNullException(nameof(rewardInfo));
        _rewardId = rewardId ?? throw new ArgumentNullException(nameof(rewardId));
    }

    // Use the actual user who redeemed the reward, not ChatBot
    public string Username => _rewardInfo.ActualAuthorName;
    public string Text => _message.GetText();
    public bool IsReward => true;
    public string? RewardId => _rewardId;

    /// <summary>Gets the underlying VKChatMessage.</summary>
    public VKChatMessage Original => _message;

    /// <summary>Gets the parsed reward information.</summary>
    public VKParsedRewardInfo RewardInfo => _rewardInfo;
}

/// <summary>
/// Adapter for Twitch custom rewards.
/// </summary>
public class TwitchRewardAdapter : IStreamReward
{
    private readonly CustomReward _reward;

    public TwitchRewardAdapter(CustomReward reward)
    {
        _reward = reward ?? throw new ArgumentNullException(nameof(reward));
    }

    public string Id => _reward.id ?? string.Empty;
    public string Title => _reward.title ?? string.Empty;

    /// <summary>Gets the underlying Twitch CustomReward.</summary>
    public CustomReward Original => _reward;
}

/// <summary>
/// Adapter for VK Play rewards.
/// </summary>
public class VKRewardAdapter : IStreamReward
{
    private readonly VKReward _reward;

    public VKRewardAdapter(VKReward reward)
    {
        _reward = reward ?? throw new ArgumentNullException(nameof(reward));
    }

    public string Id => _reward.Id ?? string.Empty;
    public string Title => _reward.Title ?? string.Empty;

    /// <summary>Gets the underlying VKReward.</summary>
    public VKReward Original => _reward;
}
