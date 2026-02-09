using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services.TtsApi;

namespace StreamTalkerClient.Services;

/// <summary>
/// Facade for all TTS server API operations. Composes focused sub-clients
/// (<see cref="TtsVoiceClient"/>, <see cref="TtsModelClient"/>,
/// <see cref="TtsSynthesisClient"/>, <see cref="TtsEnvironmentClient"/>)
/// and delegates calls to them. Manages the shared <see cref="HttpClient"/>
/// and propagates base URL changes to all sub-clients.
/// </summary>
public class QwenTtsClient : IDisposable
{
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromMinutes(5);

    private readonly ILogger<QwenTtsClient> _logger;
    private readonly HttpClient _http;
    private bool _disposed;

    /// <summary>Focused client for voice management operations.</summary>
    public ITtsVoiceClient Voices { get; }

    /// <summary>Focused client for model lifecycle operations.</summary>
    public ITtsModelClient Models { get; }

    /// <summary>Focused client for speech synthesis operations.</summary>
    public ITtsSynthesisClient Synthesis { get; }

    /// <summary>Focused client for environment/GPU queries.</summary>
    public ITtsEnvironmentClient Environment { get; }

    // Internal sub-client references for SetBaseUrl propagation
    private readonly TtsVoiceClient _voiceClient;
    private readonly TtsModelClient _modelClient;
    private readonly TtsSynthesisClient _synthesisClient;
    private readonly TtsEnvironmentClient _environmentClient;

    /// <summary>
    /// Initializes the TTS client facade, creating all sub-clients with a shared HTTP connection.
    /// </summary>
    /// <param name="baseUrl">Base URL of the TTS server (e.g. "http://localhost:7860").</param>
    public QwenTtsClient(string baseUrl = "http://localhost:7860")
    {
        _logger = AppLoggerFactory.CreateLogger<QwenTtsClient>();
        var normalizedUrl = TtsApiBase.ValidateAndNormalizeUrl(baseUrl);

        _http = new HttpClient { Timeout = DefaultRequestTimeout };

        _voiceClient = new TtsVoiceClient(_http, normalizedUrl);
        _modelClient = new TtsModelClient(_http, normalizedUrl);
        _synthesisClient = new TtsSynthesisClient(_http, normalizedUrl);
        _environmentClient = new TtsEnvironmentClient(_http, normalizedUrl);

        Voices = _voiceClient;
        Models = _modelClient;
        Synthesis = _synthesisClient;
        Environment = _environmentClient;
    }

    /// <summary>
    /// Updates the TTS server base URL across all sub-clients.
    /// </summary>
    public void SetBaseUrl(string baseUrl)
    {
        var normalized = TtsApiBase.ValidateAndNormalizeUrl(baseUrl);
        _voiceClient.SetBaseUrl(normalized);
        _modelClient.SetBaseUrl(normalized);
        _synthesisClient.SetBaseUrl(normalized);
        _environmentClient.SetBaseUrl(normalized);
        _logger.LogDebug("TTS base URL changed to: {BaseUrl}", normalized);
    }

    // ═══════════════════════════════════════════════════════════
    //  FACADE DELEGATION — Voice Operations
    // ═══════════════════════════════════════════════════════════

    /// <inheritdoc cref="ITtsVoiceClient.GetVoicesAsync"/>
    public Task<List<VoiceInfo>> GetVoicesAsync(CancellationToken ct = default) =>
        _voiceClient.GetVoicesAsync(ct);

    /// <inheritdoc cref="ITtsVoiceClient.GetVoicesDetailedAsync"/>
    public Task<List<ApiVoiceInfo>> GetVoicesDetailedAsync(CancellationToken ct = default) =>
        _voiceClient.GetVoicesDetailedAsync(ct);

    /// <inheritdoc cref="ITtsVoiceClient.CreateVoiceAsync"/>
    public Task<(bool Success, string Message)> CreateVoiceAsync(
        string voiceName, string audioFilePath, string? transcription = null,
        bool overwrite = false, bool disableTranscription = false, CancellationToken ct = default) =>
        _voiceClient.CreateVoiceAsync(voiceName, audioFilePath, transcription, overwrite, disableTranscription, ct);

    /// <inheritdoc cref="ITtsVoiceClient.DeleteVoiceAsync"/>
    public Task<(bool Success, string Message)> DeleteVoiceAsync(string voiceName, CancellationToken ct = default) =>
        _voiceClient.DeleteVoiceAsync(voiceName, ct);

    /// <inheritdoc cref="ITtsVoiceClient.RenameVoiceAsync"/>
    public Task<(bool Success, string Message)> RenameVoiceAsync(string currentName, string newName, CancellationToken ct = default) =>
        _voiceClient.RenameVoiceAsync(currentName, newName, ct);

    /// <inheritdoc cref="ITtsVoiceClient.ClearVoiceCacheAsync"/>
    public Task<(bool Success, string Message)> ClearVoiceCacheAsync(string voiceName, CancellationToken ct = default) =>
        _voiceClient.ClearVoiceCacheAsync(voiceName, ct);

    /// <inheritdoc cref="ITtsVoiceClient.ClearAllVoiceCacheAsync"/>
    public Task<(bool Success, string Message)> ClearAllVoiceCacheAsync(CancellationToken ct = default) =>
        _voiceClient.ClearAllVoiceCacheAsync(ct);

    // ═══════════════════════════════════════════════════════════
    //  FACADE DELEGATION — Model Operations
    // ═══════════════════════════════════════════════════════════

    /// <inheritdoc cref="ITtsModelClient.LoadModelAsync"/>
    public Task<bool> LoadModelAsync(
        string modelId, string attention = "auto", string quantization = "none", string warmup = "none",
        string? warmupLang = null, string? warmupVoice = null, int? warmupTimeout = null,
        bool enableOptimizations = true, bool torchCompile = true, bool cudaGraphs = false,
        bool compileCodebook = true, bool fastCodebook = true, bool forceCpu = false,
        CancellationToken ct = default) =>
        _modelClient.LoadModelAsync(modelId, attention, quantization, warmup,
            warmupLang, warmupVoice, warmupTimeout,
            enableOptimizations, torchCompile, cudaGraphs, compileCodebook, fastCodebook, forceCpu, ct);

    /// <inheritdoc cref="ITtsModelClient.UnloadModelAsync"/>
    public Task<bool> UnloadModelAsync(string modelId, CancellationToken ct = default) =>
        _modelClient.UnloadModelAsync(modelId, ct);

    /// <inheritdoc cref="ITtsModelClient.SetAutoUnloadAsync"/>
    public Task<bool> SetAutoUnloadAsync(string modelId, int minutes, CancellationToken ct = default) =>
        _modelClient.SetAutoUnloadAsync(modelId, minutes, ct);

    /// <inheritdoc cref="ITtsModelClient.GetModelsStatusAsync"/>
    public Task<ModelsStatusResponse?> GetModelsStatusAsync(CancellationToken ct = default) =>
        _modelClient.GetModelsStatusAsync(ct);

    // ═══════════════════════════════════════════════════════════
    //  FACADE DELEGATION — Synthesis Operations
    // ═══════════════════════════════════════════════════════════

    /// <inheritdoc cref="ITtsSynthesisClient.SynthesizeBatchAsync"/>
    public Task<Dictionary<int, byte[]>?> SynthesizeBatchAsync(
        string[] texts, string voice, string model = "1.7B", string language = "Auto", bool doSample = true,
        double? speed = null, double? temperature = null, int? maxNewTokens = null, double? repetitionPenalty = null,
        CancellationToken cancellationToken = default) =>
        _synthesisClient.SynthesizeBatchAsync(texts, voice, model, language, doSample,
            speed, temperature, maxNewTokens, repetitionPenalty, cancellationToken);

    /// <inheritdoc cref="ITtsSynthesisClient.SynthesizeSingleAsync"/>
    public Task<byte[]?> SynthesizeSingleAsync(
        string text, string voice, string model = "1.7B", string language = "Auto", bool doSample = true,
        double? speed = null, double? temperature = null, int? maxNewTokens = null, double? repetitionPenalty = null,
        CancellationToken cancellationToken = default) =>
        _synthesisClient.SynthesizeSingleAsync(text, voice, model, language, doSample,
            speed, temperature, maxNewTokens, repetitionPenalty, cancellationToken);

    /// <inheritdoc cref="ITtsSynthesisClient.SkipInferenceAsync"/>
    public Task<bool> SkipInferenceAsync(CancellationToken ct = default) =>
        _synthesisClient.SkipInferenceAsync(ct);

    /// <inheritdoc cref="ITtsSynthesisClient.CheckHealthAsync"/>
    public Task<bool> CheckHealthAsync(CancellationToken ct = default) =>
        _synthesisClient.CheckHealthAsync(ct);

    // ═══════════════════════════════════════════════════════════
    //  FACADE DELEGATION — Environment Operations
    // ═══════════════════════════════════════════════════════════

    /// <inheritdoc cref="ITtsEnvironmentClient.GetGpuUsageAsync"/>
    public Task<GpuUsageResponse?> GetGpuUsageAsync(CancellationToken ct = default) =>
        _environmentClient.GetGpuUsageAsync(ct);

    /// <inheritdoc cref="ITtsEnvironmentClient.GetMaxVramAsync"/>
    public Task<MaxVramResponse?> GetMaxVramAsync(CancellationToken ct = default) =>
        _environmentClient.GetMaxVramAsync(ct);

    /// <inheritdoc cref="ITtsEnvironmentClient.SetMaxVramAsync"/>
    public Task<SetMaxVramResponse?> SetMaxVramAsync(int maxVramMb, CancellationToken ct = default) =>
        _environmentClient.SetMaxVramAsync(maxVramMb, ct);

    /// <inheritdoc cref="ITtsEnvironmentClient.GetInferenceTimeoutAsync"/>
    public Task<int?> GetInferenceTimeoutAsync(CancellationToken ct = default) =>
        _environmentClient.GetInferenceTimeoutAsync(ct);

    /// <inheritdoc cref="ITtsEnvironmentClient.SetInferenceTimeoutAsync"/>
    public Task<bool> SetInferenceTimeoutAsync(int timeoutSeconds, CancellationToken ct = default) =>
        _environmentClient.SetInferenceTimeoutAsync(timeoutSeconds, ct);

    // ═══════════════════════════════════════════════════════════
    //  DISPOSAL
    // ═══════════════════════════════════════════════════════════

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _http.Dispose();
        }
        _disposed = true;
    }
}

#region API Request/Response Models

public class SynthesizeRequest
{
    [JsonPropertyName("text")]
    public object Text { get; set; } = "";

    [JsonPropertyName("voice")]
    public string Voice { get; set; } = "";

    [JsonPropertyName("model")]
    public string? Model { get; set; } = "1.7B";

    [JsonPropertyName("language")]
    public string? Language { get; set; } = "Auto";

    [JsonPropertyName("do_sample")]
    public bool DoSample { get; set; } = true;

    [JsonPropertyName("speed")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Speed { get; set; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; set; }

    [JsonPropertyName("max_new_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxNewTokens { get; set; }

    [JsonPropertyName("repetition_penalty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? RepetitionPenalty { get; set; }
}

public class AutoUnloadRequest
{
    [JsonPropertyName("minutes")]
    public int Minutes { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class VoiceListResponse
{
    [JsonPropertyName("voices")]
    public List<ApiVoiceInfo>? Voices { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class ApiVoiceInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }

    [JsonPropertyName("transcription")]
    public string? Transcription { get; set; }

    [JsonPropertyName("transcription_type")]
    public string? TranscriptionType { get; set; }

    [JsonPropertyName("cached_models")]
    public List<string>? CachedModels { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class ModelsStatusResponse
{
    [JsonPropertyName("models")]
    public Dictionary<string, ModelStatusInfo>? Models { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class ModelStatusInfo
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "unloaded";

    [JsonPropertyName("auto_unload_minutes")]
    public int AutoUnloadMinutes { get; set; }

    [JsonPropertyName("inactive_since")]
    public string? InactiveSince { get; set; }

    [JsonIgnore]
    public bool IsLoaded => Status == "ready";

    [JsonIgnore]
    public bool IsLoading => Status == "loading" || Status == "warming_up";

    [JsonIgnore]
    public bool IsWarmingUp => Status == "warming_up";
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class ModelOperationResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("model_id")]
    public string? ModelId { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class VoiceCreateResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("voice")]
    public ApiVoiceInfo? Voice { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class VoiceDeleteResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("voice_name")]
    public string? VoiceName { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class VoiceRenameRequest
{
    [JsonPropertyName("new_name")]
    public string NewName { get; set; } = "";
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class VoiceRenameResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("old_name")]
    public string? OldName { get; set; }

    [JsonPropertyName("new_name")]
    public string? NewName { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class GpuUsageResponse
{
    [JsonPropertyName("total_mb")]
    public double TotalMb { get; set; }

    [JsonPropertyName("used_mb")]
    public double UsedMb { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class MaxVramResponse
{
    [JsonPropertyName("max_vram_mb")]
    public double MaxVramMb { get; set; }

    [JsonPropertyName("total_vram_mb")]
    public double TotalVramMb { get; set; }

    [JsonPropertyName("usage_percent")]
    public double UsagePercent { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class SetMaxVramResponse
{
    [JsonPropertyName("max_vram_mb")]
    public double MaxVramMb { get; set; }

    [JsonPropertyName("saved")]
    public bool Saved { get; set; }
}

[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
    System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
public class InferenceTimeoutResponse
{
    [JsonPropertyName("timeout_seconds")]
    public int TimeoutSeconds { get; set; }
}

#endregion
