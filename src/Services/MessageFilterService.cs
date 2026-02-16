using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Managers;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Services;

/// <summary>
/// Filters incoming streaming platform messages and rewards, determining whether
/// they should be queued for TTS synthesis. Applies voice bindings, reward selection,
/// and "read all messages" / "require voice" filter rules.
/// </summary>
/// <remarks>
/// This service was extracted from MainWindowViewModel to separate message filtering
/// logic from the UI layer. It takes filter parameters as method arguments so the
/// ViewModel can pass its current UI state without coupling.
/// </remarks>
public class MessageFilterService
{
    private readonly ILogger<MessageFilterService> _logger;
    private readonly AppSettings _settings;
    private readonly MessageQueueManager _queueManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageFilterService"/> class.
    /// </summary>
    /// <param name="settings">Application settings for voice binding lookups.</param>
    /// <param name="queueManager">Queue manager to add accepted messages to.</param>
    public MessageFilterService(AppSettings settings, MessageQueueManager queueManager)
    {
        _logger = AppLoggerFactory.CreateLogger<MessageFilterService>();
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
    }

    /// <summary>
    /// Handles a regular (non-reward) chat message from a streaming platform.
    /// Applies reward-index filter, voice bindings, and "read all messages" logic.
    /// </summary>
    /// <param name="message">The incoming stream message.</param>
    /// <param name="platform">Platform name for logging ("Twitch" or "VKPlay").</param>
    /// <param name="readAllMessages">Whether "read all messages" mode is enabled for this platform.</param>
    /// <param name="requireVoice">Whether a voice prefix is required for this platform.</param>
    /// <param name="selectedRewardIndex">The currently selected reward index (0 = read all, >0 = specific reward).</param>
    /// <param name="rewardId">Reserved for future use. Currently unused.</param>
    public void HandleStreamMessage(
        IStreamMessage message,
        string platform,
        bool readAllMessages,
        bool requireVoice,
        int selectedRewardIndex,
        string? rewardId)
    {
        // Blacklist check
        if (_settings.Voice.IsBlacklisted(message.Username, platform))
            return;

        // If a specific reward is selected (index > 0), only process reward messages
        if (selectedRewardIndex > 0)
            return;

        // "Read all messages" mode
        if (readAllMessages || selectedRewardIndex == 0)
        {
            // Check for voice binding first
            var binding = _settings.Voice.GetActiveBinding(message.Username, platform);
            if (binding != null)
            {
                _queueManager.AddMessageWithBinding(message.Text, message.Username, binding.VoiceName);
                return;
            }

            // Normal message processing
            if (!readAllMessages)
                return;

            // Apply platform-specific requireVoice filter
            _queueManager.AddMessage(message.Text, message.Username, requireVoice);
        }
    }

    /// <summary>
    /// Handles a reward (channel points redemption) message from a streaming platform.
    /// Applies voice bindings first (bypasses reward filter), then checks whether
    /// the reward matches the user's selected reward filter.
    /// </summary>
    /// <param name="message">The incoming reward stream message.</param>
    /// <param name="platform">Platform name for logging ("Twitch" or "VKPlay").</param>
    /// <param name="selectedRewardIndex">The currently selected reward index (0 = read all, >0 = specific reward).</param>
    /// <param name="rewards">The list of available rewards for this platform.</param>
    public void HandleStreamReward(
        IStreamMessage message,
        string platform,
        int selectedRewardIndex,
        IReadOnlyList<IStreamReward> rewards)
    {
        // Blacklist check
        if (_settings.Voice.IsBlacklisted(message.Username, platform))
            return;

        // Check voice binding first (bypasses reward filter)
        var binding = _settings.Voice.GetActiveBinding(message.Username, platform);
        if (binding != null)
        {
            _queueManager.AddMessageWithBinding(message.Text, message.Username, binding.VoiceName);
            return;
        }

        // If "read all messages" is selected (index 0), process all rewards
        if (selectedRewardIndex == 0)
        {
            _queueManager.AddMessage(message.Text, message.Username);
            return;
        }

        // Check if this reward matches the selected reward
        if (selectedRewardIndex > 0 && selectedRewardIndex <= rewards.Count)
        {
            var selectedReward = rewards[selectedRewardIndex - 1];
            if (message.RewardId == selectedReward.Id)
            {
                _queueManager.AddMessage(message.Text, message.Username);
            }
        }
    }
}
