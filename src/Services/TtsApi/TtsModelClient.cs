using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;

namespace StreamTalkerClient.Services.TtsApi;

/// <summary>
/// Handles TTS model lifecycle API calls: load, unload, auto-unload, and status queries.
/// </summary>
public class TtsModelClient : TtsApiBase, ITtsModelClient
{
    /// <summary>
    /// Initializes a new model client sharing the given HTTP client and base URL.
    /// </summary>
    public TtsModelClient(HttpClient http, string baseUrl)
        : base(http, baseUrl, AppLoggerFactory.CreateLogger<TtsModelClient>())
    {
    }

    /// <inheritdoc />
    public async Task<bool> LoadModelAsync(
        string modelId,
        string attention = "auto", string quantization = "none", string warmup = "none",
        string? warmupLang = null, string? warmupVoice = null, int? warmupTimeout = null,
        bool enableOptimizations = true, bool torchCompile = true, bool cudaGraphs = false,
        bool compileCodebook = true, bool fastCodebook = true, bool forceCpu = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation(
                "Loading model: {ModelId} with attention: {Attention}, quantization: {Quantization}, warmup: {Warmup}",
                modelId, attention, quantization, warmup);

            var url = $"{BaseUrl}/models/{modelId}/load?attention={Uri.EscapeDataString(attention)}&quantization={Uri.EscapeDataString(quantization)}";

            if (warmup != "none")
            {
                url += $"&warmup={Uri.EscapeDataString(warmup)}";
                if (!string.IsNullOrEmpty(warmupLang))
                    url += $"&warmup_lang={Uri.EscapeDataString(warmupLang)}";
                if (!string.IsNullOrEmpty(warmupVoice))
                    url += $"&warmup_voice={Uri.EscapeDataString(warmupVoice)}";
                if (warmupTimeout.HasValue)
                    url += $"&warmup_timeout={warmupTimeout.Value}";
            }

            // Only send optimization flags when they differ from API defaults (all default true)
            if (!enableOptimizations) url += "&enable_optimizations=false";
            if (!torchCompile) url += "&torch_compile=false";
            if (cudaGraphs) url += "&cuda_graphs=true";
            if (!compileCodebook) url += "&compile_codebook=false";
            if (!fastCodebook) url += "&fast_codebook=false";
            if (forceCpu) url += "&force_cpu=true";

            var response = await Http.PostAsync(url, null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Model load request returned {StatusCode}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync(
                Infrastructure.AppJsonSerializerContext.Default.ModelOperationResponse, cancellationToken);
            var success = result?.Success ?? false;

            if (success)
                Logger.LogInformation("Model {ModelId} loaded successfully", modelId);
            else
                Logger.LogWarning("Model {ModelId} load returned success=false", modelId);

            return success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load model {ModelId}", modelId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UnloadModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Unloading model: {ModelId}", modelId);
            var response = await Http.PostAsync($"{BaseUrl}/models/{modelId}/unload", null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Model unload request returned {StatusCode}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync(
                Infrastructure.AppJsonSerializerContext.Default.ModelOperationResponse, cancellationToken);
            var success = result?.Success ?? false;

            if (success)
                Logger.LogInformation("Model {ModelId} unloaded successfully", modelId);

            return success;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to unload model {ModelId}", modelId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetAutoUnloadAsync(string modelId, int minutes, CancellationToken cancellationToken = default)
    {
        try
        {
            var content = JsonContent.Create(new AutoUnloadRequest { Minutes = minutes });
            var response = await Http.PostAsync($"{BaseUrl}/models/{modelId}/auto-unload", content, cancellationToken);

            if (response.IsSuccessStatusCode)
                Logger.LogDebug("Set auto-unload for {ModelId} to {Minutes} minutes", modelId, minutes);
            else
                Logger.LogWarning("Auto-unload request returned {StatusCode}", response.StatusCode);

            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to set auto-unload for model {ModelId}", modelId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<ModelsStatusResponse?> GetModelsStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.GetAsync($"{BaseUrl}/models/status", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync(
                Infrastructure.AppJsonSerializerContext.Default.ModelsStatusResponse, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get models status");
            return null;
        }
    }
}
