using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Services.TtsApi;

/// <summary>
/// Handles TTS voice management API calls: list, create, delete, and rename voices.
/// </summary>
public class TtsVoiceClient : TtsApiBase, ITtsVoiceClient
{
    /// <summary>
    /// Initializes a new voice client sharing the given HTTP client and base URL.
    /// </summary>
    public TtsVoiceClient(HttpClient http, string baseUrl)
        : base(http, baseUrl, AppLoggerFactory.CreateLogger<TtsVoiceClient>())
    {
    }

    /// <inheritdoc />
    public async Task<List<VoiceInfo>> GetVoicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.GetAsync($"{BaseUrl}/voices", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync(
                Infrastructure.AppJsonSerializerContext.Default.VoiceListResponse, cancellationToken);
            if (result?.Voices == null)
                return [];

            var voices = result.Voices.Select(v => new VoiceInfo
            {
                Name = v.Name ?? "",
                Description = v.Transcription,
                CachedModels = v.CachedModels
            }).ToList();

            Logger.LogDebug("Loaded {Count} voices from TTS server", voices.Count);
            return voices;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get voices from TTS server");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<List<ApiVoiceInfo>> GetVoicesDetailedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await Http.GetAsync($"{BaseUrl}/voices", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync(
                Infrastructure.AppJsonSerializerContext.Default.VoiceListResponse, cancellationToken);
            return result?.Voices ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to get detailed voices from TTS server");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> CreateVoiceAsync(
        string voiceName, string audioFilePath, string? transcription = null,
        bool overwrite = false, bool disableTranscription = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Creating voice: {VoiceName} from file: {FilePath}, overwrite: {Overwrite}",
                voiceName, audioFilePath, overwrite);

            using var form = new MultipartFormDataContent();

            var fileBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
            var fileContent = new ByteArrayContent(fileBytes);
            var fileName = Path.GetFileName(audioFilePath);
            var mimeType = GetAudioMimeType(audioFilePath);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
            form.Add(fileContent, "file", fileName);

            if (!string.IsNullOrWhiteSpace(transcription))
                form.Add(new StringContent(transcription), "transcription");

            if (overwrite)
                form.Add(new StringContent("true"), "overwrite");

            if (disableTranscription)
                form.Add(new StringContent("true"), "disable_transcription");

            var response = await Http.PostAsync(
                $"{BaseUrl}/voices/{Uri.EscapeDataString(voiceName)}", form, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                return (false, "Voice with this name already exists");

            var result = await response.Content.ReadFromJsonAsync(
                Infrastructure.AppJsonSerializerContext.Default.VoiceCreateResponse, cancellationToken);

            if (result?.Success == true)
            {
                Logger.LogInformation("Voice {VoiceName} created successfully", voiceName);
                return (true, result.Message ?? "Voice created successfully");
            }

            return (false, result?.Message ?? "Failed to create voice");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create voice {VoiceName}", voiceName);
            return (false, $"Error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> DeleteVoiceAsync(
        string voiceName, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Deleting voice: {VoiceName}", voiceName);

            var response = await Http.DeleteAsync(
                $"{BaseUrl}/voices/{Uri.EscapeDataString(voiceName)}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                return (false, "Voice is in use and cannot be deleted");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (false, "Voice not found");

            var result = await response.Content.ReadFromJsonAsync(
                Infrastructure.AppJsonSerializerContext.Default.VoiceDeleteResponse, cancellationToken);

            if (result?.Success == true)
            {
                Logger.LogInformation("Voice {VoiceName} deleted successfully", voiceName);
                return (true, result.Message ?? "Voice deleted successfully");
            }

            return (false, result?.Message ?? "Failed to delete voice");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete voice {VoiceName}", voiceName);
            return (false, $"Error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> RenameVoiceAsync(
        string currentName, string newName, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Renaming voice: {CurrentName} -> {NewName}", currentName, newName);

            var request = new VoiceRenameRequest { NewName = newName };
            var content = JsonContent.Create(request);

            var httpRequest = new HttpRequestMessage(
                new HttpMethod("PATCH"),
                $"{BaseUrl}/voices/{Uri.EscapeDataString(currentName)}/rename");
            httpRequest.Content = content;

            var response = await Http.SendAsync(httpRequest, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return (false, "Voice not found");

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                return (false, "Voice with this name already exists");

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                return (false, "Invalid voice name");

            var result = await response.Content.ReadFromJsonAsync(
                Infrastructure.AppJsonSerializerContext.Default.VoiceRenameResponse, cancellationToken);

            if (result?.Success == true)
            {
                Logger.LogInformation("Voice {CurrentName} renamed to {NewName} successfully", currentName, newName);
                return (true, result.Message ?? "Voice renamed successfully");
            }

            return (false, result?.Message ?? "Failed to rename voice");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to rename voice {CurrentName}", currentName);
            return (false, $"Error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> ClearVoiceCacheAsync(
        string voiceName, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Clearing cache for voice: {VoiceName}", voiceName);

            var response = await Http.PostAsync(
                $"{BaseUrl}/voices/clear-prompt-cache?voice_name={Uri.EscapeDataString(voiceName)}",
                null, cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            Logger.LogInformation("Cache cleared for voice {VoiceName}: {Response}", voiceName, content);

            return (true, $"Cache cleared for voice '{voiceName}'");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to clear cache for voice {VoiceName}", voiceName);
            return (false, $"Error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> ClearAllVoiceCacheAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Clearing cache for all voices");

            var response = await Http.PostAsync(
                $"{BaseUrl}/voices/clear-prompt-cache",
                null, cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            Logger.LogInformation("Cache cleared for all voices: {Response}", content);

            return (true, "Cache cleared for all voices");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to clear cache for all voices");
            return (false, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the MIME type for the given audio file based on its extension.
    /// </summary>
    private static string GetAudioMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream"
        };
    }
}
