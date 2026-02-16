using System.Text.Json;
using System.Text.Json.Serialization;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;
using StreamTalkerClient.Managers;
using StreamTalkerClient.Classes.APIModels.TwitchGQL;

namespace StreamTalkerClient.Infrastructure;

/// <summary>
/// JSON serializer context for source generation (required for trimmed builds).
/// All types that need JSON serialization must be registered here.
/// </summary>
// TTS API responses
[JsonSerializable(typeof(VoiceListResponse))]
[JsonSerializable(typeof(ApiVoiceInfo))]
[JsonSerializable(typeof(ModelsStatusResponse))]
[JsonSerializable(typeof(ModelStatusInfo))]
[JsonSerializable(typeof(ModelOperationResponse))]
[JsonSerializable(typeof(VoiceCreateResponse))]
[JsonSerializable(typeof(VoiceDeleteResponse))]
[JsonSerializable(typeof(VoiceRenameRequest))]
[JsonSerializable(typeof(VoiceRenameResponse))]
[JsonSerializable(typeof(GpuUsageResponse))]
[JsonSerializable(typeof(MaxVramResponse))]
[JsonSerializable(typeof(SetMaxVramResponse))]
[JsonSerializable(typeof(InferenceTimeoutResponse))]
// GitHub API models
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubAsset))]
[JsonSerializable(typeof(List<GitHubAsset>))]
[JsonSerializable(typeof(ServerInfoResponse))]
// App data
[JsonSerializable(typeof(BlacklistEntry))]
[JsonSerializable(typeof(List<BlacklistEntry>))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(AppSettings.PlatformServicesSettings))]
[JsonSerializable(typeof(AppSettings.PlatformSettings))]
[JsonSerializable(typeof(AppSettings.VoiceSettings))]
[JsonSerializable(typeof(AppSettings.AudioSettings))]
[JsonSerializable(typeof(AppSettings.TtsServerSettings))]
[JsonSerializable(typeof(AppSettings.ModelSettings))]
[JsonSerializable(typeof(AppSettings.ModelCoreSettings))]
[JsonSerializable(typeof(AppSettings.AutoUnloadSettings))]
[JsonSerializable(typeof(AppSettings.OptimizationSettings))]
[JsonSerializable(typeof(AppSettings.WarmupSettings))]
[JsonSerializable(typeof(AppSettings.InferenceSettings))]
[JsonSerializable(typeof(AppSettings.CacheSettings))]
[JsonSerializable(typeof(AppSettings.HotkeySettings))]
[JsonSerializable(typeof(AppSettings.UiSettings))]
[JsonSerializable(typeof(AppSettings.MetadataSettings))]
[JsonSerializable(typeof(CacheEntry))]
// VK Play models
[JsonSerializable(typeof(VKChannelResponse))]
[JsonSerializable(typeof(VKChannelData))]
[JsonSerializable(typeof(VKOwnerInfo))]
[JsonSerializable(typeof(VKWebSocketInfo))]
[JsonSerializable(typeof(VKChatMessage))]
[JsonSerializable(typeof(VKChatAuthor))]
[JsonSerializable(typeof(VKMessagePart))]
[JsonSerializable(typeof(VKParsedRewardInfo))]
[JsonSerializable(typeof(VKRewardData))]
[JsonSerializable(typeof(VKReward))]
[JsonSerializable(typeof(VKRewardsResponse))]
[JsonSerializable(typeof(VKCentrifugoMessage))]
[JsonSerializable(typeof(VKChatMessage[]))]
// Twitch models
[JsonSerializable(typeof(RewardsRoot))]
[JsonSerializable(typeof(Data))]
[JsonSerializable(typeof(Community))]
[JsonSerializable(typeof(Channel))]
[JsonSerializable(typeof(Self))]
[JsonSerializable(typeof(CommunityPointsSettings))]
[JsonSerializable(typeof(CustomReward))]
[JsonSerializable(typeof(AutomaticReward))]
[JsonSerializable(typeof(DefaultImage))]
[JsonSerializable(typeof(Image))]
[JsonSerializable(typeof(MaxPerStreamSetting))]
[JsonSerializable(typeof(MaxPerUserPerStreamSetting))]
[JsonSerializable(typeof(GlobalCooldownSetting))]
[JsonSerializable(typeof(Extensions))]
[JsonSerializable(typeof(EmoteVariant))]
[JsonSerializable(typeof(Emote))]
[JsonSerializable(typeof(Modification))]
[JsonSerializable(typeof(Modifier))]
[JsonSerializable(typeof(RewardsRoot[]))]
// Collections and primitives
[JsonSerializable(typeof(Dictionary<string, ModelStatusInfo>))]
[JsonSerializable(typeof(Dictionary<string, CacheEntry>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<ApiVoiceInfo>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
