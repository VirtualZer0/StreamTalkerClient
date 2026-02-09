using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Services;

/// <summary>
/// Service for connecting to VK Play Live chat via custom WebSocket client.
/// Uses JSON protocol with proper Origin header for anonymous readonly access.
/// Mirrors the TwitchService pattern for consistency.
/// </summary>
public class VKPlayService : StreamingServiceBase
{
    private readonly VKPlayApiClient _apiClient;
    private VKPlayWebSocket? _webSocket;
    private VKReward[] _customRewards = [];
    private IReadOnlyList<IStreamReward>? _cachedRewards;

    /// <summary>
    /// The VK Play channel ID (numeric).
    /// </summary>
    public string? ChannelId { get; private set; }

    /// <summary>
    /// Raw VK Play rewards array for direct access.
    /// </summary>
    public VKReward[] CustomRewards
    {
        get => _customRewards;
        set
        {
            _customRewards = value;
            _cachedRewards = null; // Invalidate cache
        }
    }

    /// <summary>
    /// Unified rewards interface with caching to avoid allocations on every access.
    /// </summary>
    public override IReadOnlyList<IStreamReward> Rewards =>
        _cachedRewards ??= _customRewards.Select(r => new VKRewardAdapter(r)).ToList();

    public VKPlayService() : base(AppLoggerFactory.CreateLogger<VKPlayService>())
    {
        _apiClient = new VKPlayApiClient();
    }

    /// <summary>
    /// Joins a VK Play Live channel and starts receiving chat messages.
    /// </summary>
    /// <param name="channelName">The channel name (blog URL slug, e.g., "lebwa")</param>
    public override async Task JoinChannelAsync(string channelName)
    {
        try
        {
            Channel = channelName;
            SetConnectionState(StreamConnectionState.Connecting);

            // Clean up any existing connection
            await DisconnectInternalAsync();

            _logger.LogInformation("Getting channel info for {Channel}", channelName);

            // Get channel info from API
            var channelInfo = await _apiClient.GetChannelInfoAsync(channelName);
            if (channelInfo == null)
            {
                _logger.LogError("Failed to get channel info for {Channel}", channelName);
                SetConnectionState(StreamConnectionState.Error);
                return;
            }

            ChannelId = channelInfo.Id;
            _logger.LogInformation("Channel ID: {ChannelId}, Online: {IsOnline}", ChannelId, channelInfo.IsOnline);

            // Get chat channel name for subscription
            string chatChannel;
            try
            {
                chatChannel = _apiClient.GetChatChannelName(channelInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to determine chat channel name");
                SetConnectionState(StreamConnectionState.Error);
                return;
            }

            _logger.LogInformation("Chat channel: {ChatChannel}", chatChannel);

            // Get anonymous WebSocket token
            _logger.LogInformation("Getting WebSocket token...");
            var wsToken = await _apiClient.GetWebSocketTokenAsync();
            if (string.IsNullOrEmpty(wsToken))
            {
                _logger.LogError("Failed to get WebSocket token");
                SetConnectionState(StreamConnectionState.Error);
                return;
            }
            _logger.LogInformation("Got WebSocket token");

            // Create custom WebSocket client
            _webSocket = new VKPlayWebSocket();
            _webSocket.OnConnected += WebSocket_OnConnected;
            _webSocket.OnDisconnected += WebSocket_OnDisconnected;
            _webSocket.OnError += WebSocket_OnError;
            _webSocket.OnMessage += WebSocket_OnMessage;

            // Connect and subscribe with token
            await _webSocket.ConnectAsync(chatChannel, wsToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join channel {Channel}", channelName);
            SetConnectionState(StreamConnectionState.Error);
        }
    }

    /// <summary>
    /// Loads channel rewards (channel points).
    /// </summary>
    public override async Task LoadRewardsAsync(string channelName)
    {
        try
        {
            // API uses channel name, not ID
            CustomRewards = await _apiClient.GetRewardsAsync(channelName);
            _logger.LogInformation("Loaded {Count} rewards for channel {Channel}", _customRewards.Length, channelName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rewards for channel {Channel}", channelName);
            CustomRewards = [];
        }
    }

    public override async Task DisconnectAsync()
    {
        await DisconnectInternalAsync();
        SetConnectionState(StreamConnectionState.Disconnected);
    }

    private async Task DisconnectInternalAsync()
    {
        try
        {
            if (_webSocket != null)
            {
                _webSocket.OnConnected -= WebSocket_OnConnected;
                _webSocket.OnDisconnected -= WebSocket_OnDisconnected;
                _webSocket.OnError -= WebSocket_OnError;
                _webSocket.OnMessage -= WebSocket_OnMessage;
                await _webSocket.DisconnectAsync();
                _webSocket.Dispose();
                _webSocket = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect");
        }
    }

    #region WebSocket Event Handlers

    private void WebSocket_OnConnected(object? sender, EventArgs e)
    {
        _logger.LogInformation("VK Play WebSocket connected");
        SetConnectionState(StreamConnectionState.Connected);

        // After connect response, we've also subscribed, so mark as joined
        // The subscription happens automatically after connect in our implementation
        Task.Delay(500).ContinueWith(_ =>
        {
            if (_webSocket?.IsConnected == true)
            {
                SetConnectionState(StreamConnectionState.Joined);
            }
        });
    }

    private void WebSocket_OnDisconnected(object? sender, EventArgs e)
    {
        _logger.LogInformation("VK Play WebSocket disconnected");
        SetConnectionState(StreamConnectionState.Disconnected);
    }

    private void WebSocket_OnError(object? sender, string error)
    {
        _logger.LogError("VK Play WebSocket error: {Error}", error);
        SetConnectionState(StreamConnectionState.Error);
    }

    private void WebSocket_OnMessage(object? sender, VKChatMessage message)
    {
        try
        {
            var text = message.GetText();
            _logger.LogDebug("Chat message from {Author}: {Text} (IsReward={IsReward})",
                message.Author?.DisplayName, text, message.IsReward);

            // Check for ChatBot reward message format first
            var parsedReward = message.GetParsedRewardInfo();
            if (parsedReward != null)
            {
                // Find matching reward by title from loaded rewards
                var matchingReward = _customRewards.FirstOrDefault(r =>
                    string.Equals(r.Title, parsedReward.RewardTitle, StringComparison.OrdinalIgnoreCase));

                var rewardId = matchingReward?.Id ?? parsedReward.RewardTitle; // Use title as fallback ID

                _logger.LogDebug("ChatBot reward message: {RewardTitle} (ID={RewardId}) from {Author}, text: {Text}",
                    parsedReward.RewardTitle, rewardId, parsedReward.ActualAuthorName, text);

                // Create a modified message with the actual author for the adapter
                var adaptedMessage = new VKMessageAdapterWithReward(message, parsedReward, rewardId);

                RaiseReward(adaptedMessage);
                return;
            }

            // Create unified message adapter
            var normalAdaptedMessage = new VKMessageAdapter(message);

            // Check if this is a reward message (has "marked" style with reward data)
            if (message.IsReward && message.Reward != null)
            {
                _logger.LogDebug("Marked reward message: {RewardId} - {RewardTitle} from {Author}",
                    message.Reward.Id, message.Reward.Title, message.Author?.DisplayName);

                RaiseReward(normalAdaptedMessage);
            }
            else
            {
                RaiseMessage(normalAdaptedMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling VK chat message");
        }
    }

    #endregion

    protected override void DisposeCore()
    {
        Disconnect();
        _apiClient.Dispose();
    }
}
