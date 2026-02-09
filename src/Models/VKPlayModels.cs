using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamTalkerClient.Models;

/// <summary>
/// VK Play Live connection states.
/// </summary>
public enum VKConnectionState
{
    Disconnected = 0,
    InProgress,      // Getting channel info via HTTP
    Connecting,      // WebSocket connecting
    Connected,       // WebSocket connected
    Subscribed,      // Subscribed to chat channel
    Error
}

/// <summary>
/// VK Play channel information response.
/// </summary>
public class VKChannelResponse
{
    [JsonPropertyName("data")]
    public VKChannelData? Data { get; set; }
}

/// <summary>
/// VK Play channel data.
/// </summary>
public class VKChannelData
{
    [JsonPropertyName("blogUrl")]
    public string? BlogUrl { get; set; }

    [JsonPropertyName("owner")]
    public VKOwnerInfo? Owner { get; set; }

    [JsonPropertyName("publicWebSocketChannel")]
    public string? PublicWebSocketChannel { get; set; }

    [JsonPropertyName("isOnline")]
    public bool IsOnline { get; set; }

    /// <summary>
    /// Gets the channel ID from the owner.
    /// </summary>
    public string? Id => Owner?.Id.ToString();
}

/// <summary>
/// VK Play channel owner information.
/// </summary>
public class VKOwnerInfo
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("nick")]
    public string? Nick { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}

/// <summary>
/// WebSocket connection information.
/// </summary>
public class VKWebSocketInfo
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }
}

/// <summary>
/// Chat message from VK Play Live WebSocket.
/// </summary>
public class VKChatMessage
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("author")]
    public VKChatAuthor? Author { get; set; }

    [JsonPropertyName("data")]
    public List<VKMessagePart>? Data { get; set; }

    [JsonPropertyName("createdAt")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("styles")]
    public List<string>? Styles { get; set; }

    [JsonPropertyName("reward")]
    public VKRewardData? Reward { get; set; }

    /// <summary>
    /// Cached parsed reward info from ChatBot message.
    /// </summary>
    private VKParsedRewardInfo? _parsedRewardInfo;
    private bool _rewardInfoParsed = false;

    /// <summary>
    /// Checks if this is a reward/highlighted message.
    /// Supports both "marked" style and ChatBot reward format.
    /// </summary>
    public bool IsReward
    {
        get
        {
            // Check traditional "marked" style
            if (Styles?.Contains("marked") == true)
                return true;

            // Check ChatBot reward format
            var parsed = GetParsedRewardInfo();
            return parsed != null;
        }
    }

    /// <summary>
    /// Gets parsed reward info from ChatBot message format.
    /// </summary>
    public VKParsedRewardInfo? GetParsedRewardInfo()
    {
        if (_rewardInfoParsed)
            return _parsedRewardInfo;

        _rewardInfoParsed = true;
        _parsedRewardInfo = TryParseChatBotReward();
        return _parsedRewardInfo;
    }

    /// <summary>
    /// Tries to parse ChatBot reward message format.
    /// Format: [mention of user] + "получает награду: {name} за {cost}" + newline + user message
    /// </summary>
    private VKParsedRewardInfo? TryParseChatBotReward()
    {
        // Must be from ChatBot
        if (Author?.Nick != "ChatBot" && Author?.DisplayName != "ChatBot")
            return null;

        // Need at least 3 data parts: mention, reward text, user message
        if (Data == null || Data.Count < 3)
            return null;

        // First part must be a mention
        var mentionPart = Data[0];
        if (mentionPart.Type != "mention")
            return null;

        // Second part should contain "получает награду:"
        var rewardTextPart = Data[1];
        if (rewardTextPart.Type != "text" || string.IsNullOrEmpty(rewardTextPart.Content))
            return null;

        var rewardText = TryParseJsonContent(rewardTextPart.Content);
        if (!rewardText.Contains("получает награду:"))
            return null;

        // Parse reward name and cost from "получает награду: {name} за {cost}"
        var match = System.Text.RegularExpressions.Regex.Match(
            rewardText,
            @"получает награду:\s*(.+?)\s+за\s+(\d+)");

        if (!match.Success)
            return null;

        var rewardTitle = match.Groups[1].Value.Trim();
        int.TryParse(match.Groups[2].Value, out var cost);

        // Extract the actual user's display name from the mention
        var actualUserName = mentionPart.DisplayName ?? mentionPart.Name ?? mentionPart.Nick ?? "Unknown";

        return new VKParsedRewardInfo
        {
            ActualAuthorName = actualUserName,
            ActualAuthorId = mentionPart.GetNumericId() ?? 0,
            RewardTitle = rewardTitle,
            RewardCost = cost
        };
    }

    /// <summary>
    /// Extracts plain text from message parts, including smile names.
    /// Content can be plain text or JSON-encoded array: ["text","unstyled",[]]
    /// For reward messages, extracts only the user's message (not the reward notification).
    /// </summary>
    public string GetText()
    {
        if (Data == null || Data.Count == 0)
            return string.Empty;

        // Check if this is a ChatBot reward message
        var rewardInfo = GetParsedRewardInfo();
        var startIndex = 0;

        if (rewardInfo != null)
        {
            // Skip the mention, reward text, and newline parts
            // Structure: [mention, reward_text, newline, user_message_parts...]
            startIndex = 3; // Skip first 3 parts
        }

        var parts = new List<string>();
        for (int i = startIndex; i < Data.Count; i++)
        {
            var part = Data[i];
            if (part.Type == "text" && !string.IsNullOrEmpty(part.Content))
            {
                // Content might be JSON-encoded: ["actual text","unstyled",[]]
                var text = TryParseJsonContent(part.Content);
                if (!string.IsNullOrEmpty(text) && part.Modificator != "BLOCK_END")
                {
                    parts.Add(text);
                }
            }
            else if (part.Type == "smile" && !string.IsNullOrEmpty(part.Name))
            {
                // Include smile/emote name in text
                parts.Add(part.Name);
            }
            else if (part.Type == "mention")
            {
                var mentionName = part.DisplayName ?? part.Name ?? part.Nick;
                if (!string.IsNullOrEmpty(mentionName))
                {
                    parts.Add($"@{mentionName}");
                }
            }
            // Skip links for TTS
        }
        return string.Join("", parts).Trim();
    }

    private static string TryParseJsonContent(string content)
    {
        // Try to parse as JSON array: ["text","unstyled",[]]
        if (content.StartsWith("[") && content.EndsWith("]"))
        {
            try
            {
                var arr = JsonSerializer.Deserialize<JsonElement>(content);
                if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                {
                    return arr[0].GetString() ?? content;
                }
            }
            catch
            {
                // Not valid JSON, return as-is
            }
        }
        return content;
    }
}

/// <summary>
/// Chat message author information.
/// </summary>
public class VKChatAuthor
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("nick")]
    public string? Nick { get; set; }

    [JsonPropertyName("isChannelModerator")]
    public bool IsChannelModerator { get; set; }
}

/// <summary>
/// Message part (text, smile, mention, link).
/// </summary>
public class VKMessagePart
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }  // "text", "smile", "mention", "link"

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("modificator")]
    public string? Modificator { get; set; }  // "", "BLOCK_END"

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }  // Smile/emote name or mention username

    [JsonPropertyName("emoteName")]
    public string? EmoteName { get; set; }

    // Mention-specific properties
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("nick")]
    public string? Nick { get; set; }

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    /// <summary>
    /// Gets numeric ID (for mentions/users). Returns null for non-numeric IDs (like smile GUIDs).
    /// </summary>
    public long? GetNumericId()
    {
        if (Id == null) return null;
        if (Id.Value.ValueKind == JsonValueKind.Number)
            return Id.Value.GetInt64();
        if (Id.Value.ValueKind == JsonValueKind.String &&
            long.TryParse(Id.Value.GetString(), out var parsed))
            return parsed;
        return null;
    }
}

/// <summary>
/// Parsed reward info from ChatBot message format.
/// </summary>
public class VKParsedRewardInfo
{
    public string ActualAuthorName { get; set; } = "";
    public long ActualAuthorId { get; set; }
    public string RewardTitle { get; set; } = "";
    public int RewardCost { get; set; }
}

/// <summary>
/// Reward data attached to a message.
/// </summary>
public class VKRewardData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("cost")]
    public int Cost { get; set; }
}

/// <summary>
/// Channel point reward definition.
/// </summary>
public class VKReward
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("cost")]
    public int Cost { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    public override string ToString() => $"{Title} ({Cost} pts)";
}

/// <summary>
/// Rewards list response.
/// </summary>
public class VKRewardsResponse
{
    [JsonPropertyName("data")]
    public List<VKReward>? Data { get; set; }
}

/// <summary>
/// Centrifugo publication data wrapper.
/// </summary>
public class VKCentrifugoMessage
{
    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("data")]
    public VKChatMessage? Data { get; set; }
}
