using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using StreamTalkerClient.Classes.APIModels.TwitchGQL;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Services;

public class TwitchService : StreamingServiceBase
{
    private const string GqlRewardsQuery = """
        [
            {
            "operationName": "ChannelPointsContext",
            "variables": {
                    "channelLogin": "CHANNEL",
                "includeGoalTypes": [
                    "CREATOR",
                    "BOOST"
                ]
            },
            "extensions": {
                    "persistedQuery": {
                        "version": 1,
                    "sha256Hash": "1530a003a7d374b0380b79db0be0534f30ff46e61cffa2bc0e2468a909fbc024"
                    }
                }
            }
        ]
        """;

    private const string ClientId = "kimne78kx3ncx6brgo4mv6wki5h1ko";
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private TwitchClient? _client;
    private readonly Random _rnd = new();
    private CustomReward[] _customRewards = [];
    private IReadOnlyList<IStreamReward>? _cachedRewards;

    /// <summary>
    /// Raw Twitch custom rewards array for direct access.
    /// </summary>
    public CustomReward[] CustomRewards
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
        _cachedRewards ??= _customRewards.Select(r => new TwitchRewardAdapter(r)).ToList();

    public TwitchService() : base(AppLoggerFactory.CreateLogger<TwitchService>())
    {
    }

    private void InitializeClient()
    {
        var clientOptions = new ClientOptions();
        var customClient = new WebSocketClient(clientOptions);
        _client = new TwitchClient(customClient);

        var credentials = new ConnectionCredentials(
            $"{Infrastructure.AppConstants.Twitch.AnonymousUserPrefix}{_rnd.Next(Infrastructure.AppConstants.Twitch.AnonymousUserMinId, Infrastructure.AppConstants.Twitch.AnonymousUserMaxId)}",
            "");
        _client.Initialize(credentials);

        _client.OnConnected += Client_OnConnectedAsync;
        _client.OnJoinedChannel += Client_OnJoinedChannelAsync;
        _client.OnConnectionError += Client_OnConnectionErrorAsync;
        _client.OnDisconnected += Client_OnDisconnectedAsync;
        _client.OnMessageReceived += Client_OnMessageReceivedAsync;
    }

    private void CleanupClient()
    {
        if (_client != null)
        {
            _client.OnConnected -= Client_OnConnectedAsync;
            _client.OnJoinedChannel -= Client_OnJoinedChannelAsync;
            _client.OnConnectionError -= Client_OnConnectionErrorAsync;
            _client.OnDisconnected -= Client_OnDisconnectedAsync;
            _client.OnMessageReceived -= Client_OnMessageReceivedAsync;
            _client = null;
        }
    }

    private async Task Client_OnConnectedAsync(object? sender, OnConnectedEventArgs e)
    {
        SetConnectionState(StreamConnectionState.Connected);

        try
        {
            if (!string.IsNullOrEmpty(Channel))
            {
                await _client!.JoinChannelAsync(Channel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join channel {Channel}", Channel);
            SetConnectionState(StreamConnectionState.Error);
        }
    }

    private Task Client_OnJoinedChannelAsync(object? sender, OnJoinedChannelArgs e)
    {
        _logger.LogInformation("Joined channel: {Channel}", e.Channel);
        SetConnectionState(StreamConnectionState.Joined);
        return Task.CompletedTask;
    }

    private Task Client_OnConnectionErrorAsync(object? sender, OnConnectionErrorArgs e)
    {
        _logger.LogError("Twitch connection error: {Message}", e.Error.Message);
        SetConnectionState(StreamConnectionState.Error);
        return Task.CompletedTask;
    }

    private Task Client_OnDisconnectedAsync(object? sender, OnDisconnectedArgs e)
    {
        _logger.LogInformation("Disconnected from Twitch");
        SetConnectionState(StreamConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    private Task Client_OnMessageReceivedAsync(object? sender, OnMessageReceivedArgs e)
    {
        string? rewardId = null;

        if (e.ChatMessage.RawIrcMessage.Contains("custom-reward-id="))
        {
            int indexPos = e.ChatMessage.RawIrcMessage.IndexOf("custom-reward-id=");
            if (indexPos >= 0)
            {
                int pFrom = indexPos + "custom-reward-id=".Length;
                int pTo = e.ChatMessage.RawIrcMessage.IndexOf(';', pFrom);

                if (pTo > pFrom)
                {
                    rewardId = e.ChatMessage.RawIrcMessage.Substring(pFrom, pTo - pFrom);
                }
            }
        }

        // Create unified message adapter
        var message = new TwitchMessageAdapter(e.ChatMessage, rewardId);

        if (!string.IsNullOrEmpty(rewardId))
        {
            RaiseReward(message);
        }
        else
        {
            RaiseMessage(message);
        }

        return Task.CompletedTask;
    }

    public override async Task JoinChannelAsync(string channel)
    {
        try
        {
            Channel = channel;

            if (_client != null)
            {
                await DisconnectAsync();
            }

            InitializeClient();
            SetConnectionState(StreamConnectionState.Connecting);

            _logger.LogInformation("Connecting to Twitch for channel: {Channel}", channel);
            await _client!.ConnectAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to channel {Channel}", channel);
            SetConnectionState(StreamConnectionState.Error);
        }
    }

    public override async Task LoadRewardsAsync(string channel)
    {
        try
        {
            var request = GqlRewardsQuery.Replace("CHANNEL", channel).Replace("\r\n", "").Replace(" ", "");
            var content = new StringContent(request, Encoding.UTF8, "application/json");
            content.Headers.Add("Client-id", ClientId);

            var response = await SharedHttpClient.PostAsync("https://gql.twitch.tv/gql", content);
            var res = await response.Content.ReadAsStringAsync();

            var roots = JsonSerializer.Deserialize(res, Infrastructure.AppJsonSerializerContext.Default.RewardsRootArray);
            CustomRewards = roots?[0]?.data?.community?.channel?.communityPointsSettings?.customRewards ?? [];

            _logger.LogInformation("Loaded {Count} rewards for channel {Channel}", _customRewards.Length, channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rewards for channel {Channel}", channel);
            CustomRewards = [];
        }
    }

    public override async Task DisconnectAsync()
    {
        try
        {
            if (_client != null)
            {
                await _client.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect");
        }

        CleanupClient();
        SetConnectionState(StreamConnectionState.Disconnected);
    }

    protected override void DisposeCore()
    {
        CleanupClient();
    }
}
