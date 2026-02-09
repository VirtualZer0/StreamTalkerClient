using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Services;

/// <summary>
/// HTTP client for VK Play Live API.
/// Based on reverse-engineered endpoints from streamlink plugin and unofficial libraries.
/// </summary>
public class VKPlayApiClient : IDisposable
{
    private const string ApiBaseUrl = "https://api.live.vkvideo.ru/v1";
    private const string WebSocketBaseUrl = "wss://pubsub.live.vkvideo.ru/connection/websocket?cf_protocol_version=v2";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<VKPlayApiClient> _logger;
    private bool _disposed;

    public VKPlayApiClient()
    {
        _logger = AppLoggerFactory.CreateLogger<VKPlayApiClient>();
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "StreamTalkerClient/1.0");
    }

    /// <summary>
    /// Gets channel information by channel name (blog URL).
    /// </summary>
    /// <param name="channelName">The channel name (e.g., "lebwa", "play_code")</param>
    /// <returns>Channel data including WebSocket channel name, or null if not found</returns>
    public async Task<VKChannelData?> GetChannelInfoAsync(string channelName)
    {
        try
        {
            var url = $"{ApiBaseUrl}/blog/{channelName}";
            _logger.LogDebug("Getting channel info from {Url}", url);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get channel info: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Channel info response: {Json}", json);

            // API returns data directly, not wrapped
            var result = JsonSerializer.Deserialize(json, Infrastructure.AppJsonSerializerContext.Default.VKChannelData);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting channel info for {Channel}", channelName);
            return null;
        }
    }

    /// <summary>
    /// Gets WebSocket connection URL for the chat.
    /// </summary>
    public string GetWebSocketUrl()
    {
        return WebSocketBaseUrl;
    }

    /// <summary>
    /// Gets anonymous WebSocket connection token.
    /// This works without authentication and returns a guest token.
    /// </summary>
    public async Task<string?> GetWebSocketTokenAsync()
    {
        try
        {
            var url = $"{ApiBaseUrl}/ws/connect";
            _logger.LogDebug("Getting WebSocket token from {Url}", url);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get WebSocket token: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("WebSocket token response: {Json}", json);

            var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            if (result.TryGetProperty("token", out var tokenProp))
            {
                return tokenProp.GetString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting WebSocket token");
            return null;
        }
    }

    /// <summary>
    /// Gets the chat channel name for Centrifugo subscription.
    /// Format: "channel-chat:{channelId}"
    /// </summary>
    /// <param name="channelData">Channel data from GetChannelInfoAsync</param>
    /// <returns>The chat channel name to subscribe to</returns>
    public string GetChatChannelName(VKChannelData channelData)
    {
        if (!string.IsNullOrEmpty(channelData.Id))
        {
            return $"channel-chat:{channelData.Id}";
        }

        throw new InvalidOperationException("Cannot determine chat channel name - no channel ID available");
    }

    /// <summary>
    /// Gets channel rewards (channel points).
    /// Endpoint: /channel/{channelName}/point/reward/
    /// </summary>
    public async Task<VKReward[]> GetRewardsAsync(string channelName)
    {
        try
        {
            var url = $"{ApiBaseUrl}/channel/{channelName}/point/reward/";
            _logger.LogDebug("Getting rewards from {Url}", url);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Rewards endpoint returned {StatusCode}", response.StatusCode);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Rewards response: {Json}", json);

            // Parse response: { "data": { "rewards": [...] } }
            var doc = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            if (doc.TryGetProperty("data", out var data) &&
                data.TryGetProperty("rewards", out var rewards))
            {
                var rewardList = new List<VKReward>();
                foreach (var reward in rewards.EnumerateArray())
                {
                    var r = new VKReward
                    {
                        Id = reward.TryGetProperty("id", out var id) ? id.GetString() : null,
                        Title = reward.TryGetProperty("name", out var name) ? name.GetString() : null,
                        Description = reward.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                        Cost = reward.TryGetProperty("price", out var price) ? price.GetInt32() : 0,
                        IsEnabled = reward.TryGetProperty("isAvailable", out var avail) && avail.GetBoolean()
                    };
                    rewardList.Add(r);
                }
                return rewardList.ToArray();
            }

            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rewards for channel {Channel}", channelName);
            return [];
        }
    }

    /// <summary>
    /// Gets the last N messages from the chat (HTTP polling fallback).
    /// This can be used if WebSocket connection fails.
    /// </summary>
    public async Task<VKChatMessage[]> GetLastMessagesAsync(string channelId, int count = 100)
    {
        try
        {
            var url = $"{ApiBaseUrl}/blog/{channelId}/public_video_stream/chat?count={count}";
            _logger.LogDebug("Getting last messages from {Url}", url);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Chat messages endpoint returned {StatusCode}", response.StatusCode);
                return [];
            }

            var json = await response.Content.ReadAsStringAsync();

            // Try to parse as array or as wrapped response
            try
            {
                var messages = JsonSerializer.Deserialize(json, Infrastructure.AppJsonSerializerContext.Default.VKChatMessageArray);
                return messages ?? [];
            }
            catch
            {
                // Try alternative format with data wrapper
                var result = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
                if (result.TryGetProperty("data", out var data))
                {
                    return JsonSerializer.Deserialize(data.GetRawText(), Infrastructure.AppJsonSerializerContext.Default.VKChatMessageArray) ?? [];
                }
                return [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last messages for channel {Channel}", channelId);
            return [];
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}
