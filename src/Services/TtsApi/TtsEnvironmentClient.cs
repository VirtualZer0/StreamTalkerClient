using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;

namespace StreamTalkerClient.Services.TtsApi;

/// <summary>
/// Handles TTS server environment API calls: GPU usage, VRAM limits, and inference timeout.
/// </summary>
public class TtsEnvironmentClient : TtsApiBase, ITtsEnvironmentClient
{
    /// <summary>
    /// Initializes a new environment client sharing the given HTTP client and base URL.
    /// </summary>
    public TtsEnvironmentClient(HttpClient http, string baseUrl)
        : base(http, baseUrl, AppLoggerFactory.CreateLogger<TtsEnvironmentClient>())
    {
    }

    /// <inheritdoc />
    public async Task<GpuUsageResponse?> GetGpuUsageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.GetAsync($"{BaseUrl}/environment/gpu-usage", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync(
                Infrastructure.AppJsonSerializerContext.Default.GpuUsageResponse, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to get GPU usage");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<MaxVramResponse?> GetMaxVramAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.GetAsync($"{BaseUrl}/environment/max-vram", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync(
                Infrastructure.AppJsonSerializerContext.Default.MaxVramResponse, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to get max VRAM");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<SetMaxVramResponse?> SetMaxVramAsync(int maxVramMb, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Setting max VRAM to {MaxVramMb} MB", maxVramMb);
            var response = await Http.PutAsync(
                $"{BaseUrl}/environment/max-vram?max_vram_mb={maxVramMb}", null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync(
                    Infrastructure.AppJsonSerializerContext.Default.SetMaxVramResponse, cancellationToken);
                Logger.LogInformation("Max VRAM set to {MaxVramMb} MB, saved={Saved}", result?.MaxVramMb, result?.Saved);
                return result;
            }
            else
            {
                Logger.LogWarning("Set max VRAM returned {StatusCode}", response.StatusCode);
                return null;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to set max VRAM");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<int?> GetInferenceTimeoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.GetAsync($"{BaseUrl}/environment/inference-timeout", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync(
                Infrastructure.AppJsonSerializerContext.Default.InferenceTimeoutResponse, cancellationToken);
            return result?.TimeoutSeconds;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to get inference timeout");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetInferenceTimeoutAsync(int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Setting inference timeout to {Seconds} seconds", timeoutSeconds);
            var response = await Http.PutAsync(
                $"{BaseUrl}/environment/inference-timeout?timeout_seconds={timeoutSeconds}", null, cancellationToken);

            if (response.IsSuccessStatusCode)
                Logger.LogInformation("Inference timeout set to {Seconds} seconds", timeoutSeconds);
            else
                Logger.LogWarning("Set inference timeout returned {StatusCode}", response.StatusCode);

            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to set inference timeout");
            return false;
        }
    }
}
