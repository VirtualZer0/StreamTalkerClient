using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;

namespace StreamTalkerClient.Services.TtsApi;

/// <summary>
/// Handles TTS speech synthesis API calls: batch/single synthesis, skip inference, and health checks.
/// </summary>
public class TtsSynthesisClient : TtsApiBase, ITtsSynthesisClient
{
    /// <summary>Timeout for health check requests (shorter than normal requests).</summary>
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes a new synthesis client sharing the given HTTP client and base URL.
    /// </summary>
    public TtsSynthesisClient(HttpClient http, string baseUrl)
        : base(http, baseUrl, AppLoggerFactory.CreateLogger<TtsSynthesisClient>())
    {
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, byte[]>?> SynthesizeBatchAsync(
        string[] texts, string voice,
        string model = "1.7B", string language = "Auto", bool doSample = true,
        double? speed = null, double? temperature = null,
        int? maxNewTokens = null, double? repetitionPenalty = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogDebug("Synthesizing batch of {Count} texts with voice {Voice}, doSample={DoSample}",
                texts.Length, voice, doSample);

            var request = new SynthesizeRequest
            {
                Text = texts,
                Voice = voice,
                Model = model,
                Language = language,
                DoSample = doSample,
                Speed = speed,
                Temperature = temperature,
                MaxNewTokens = maxNewTokens,
                RepetitionPenalty = repetitionPenalty
            };

            var content = JsonContent.Create(request);
            using var response = await Http.PostAsync($"{BaseUrl}/synthesize_speech/", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var zipBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var result = ExtractWavFilesFromZip(zipBytes);

            Logger.LogDebug("Batch synthesis complete, extracted {Count} audio files", result.Count);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP error during batch synthesis for voice {Voice}", voice);
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to synthesize batch for voice {Voice}", voice);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> SynthesizeSingleAsync(
        string text, string voice,
        string model = "1.7B", string language = "Auto", bool doSample = true,
        double? speed = null, double? temperature = null,
        int? maxNewTokens = null, double? repetitionPenalty = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new SynthesizeRequest
            {
                Text = text,
                Voice = voice,
                Model = model,
                Language = language,
                DoSample = doSample,
                Speed = speed,
                Temperature = temperature,
                MaxNewTokens = maxNewTokens,
                RepetitionPenalty = repetitionPenalty
            };

            var content = JsonContent.Create(request);
            using var response = await Http.PostAsync($"{BaseUrl}/synthesize_speech/", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to synthesize single text with voice {Voice}", voice);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SkipInferenceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Requesting inference skip");
            var response = await Http.PostAsync($"{BaseUrl}/skip_inference", null, cancellationToken);

            if (response.IsSuccessStatusCode)
                Logger.LogInformation("Inference skip requested successfully");
            else
                Logger.LogWarning("Skip inference request returned {StatusCode}", response.StatusCode);

            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to request inference skip");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(HealthCheckTimeout);

            using var response = await Http.GetAsync($"{BaseUrl}/health", timeoutCts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.LogDebug("Health check timed out for {BaseUrl}", BaseUrl);
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Health check failed for {BaseUrl}", BaseUrl);
            return false;
        }
    }

    /// <summary>
    /// Extracts WAV files from a ZIP archive returned by the batch synthesis endpoint.
    /// Files are named by their index (e.g. "0.wav", "1.wav").
    /// </summary>
    private static Dictionary<int, byte[]> ExtractWavFilesFromZip(byte[] zipBytes)
    {
        var result = new Dictionary<int, byte[]>();

        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            if (!entry.Name.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                continue;

            var nameWithoutExt = Path.GetFileNameWithoutExtension(entry.Name);
            if (!int.TryParse(nameWithoutExt, out int index))
                continue;

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            result[index] = ms.ToArray();
        }

        return result;
    }
}
