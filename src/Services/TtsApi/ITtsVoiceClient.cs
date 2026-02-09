using StreamTalkerClient.Models;

namespace StreamTalkerClient.Services.TtsApi;

/// <summary>
/// Interface for TTS voice management operations: listing, creating, deleting, and renaming voices.
/// </summary>
public interface ITtsVoiceClient
{
    /// <summary>
    /// Gets the list of available voices (simplified info: name, description, cached models).
    /// </summary>
    Task<List<VoiceInfo>> GetVoicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the list of available voices with full API details (creation date, transcription, etc.).
    /// </summary>
    Task<List<ApiVoiceInfo>> GetVoicesDetailedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new voice from an audio file with optional transcription.
    /// </summary>
    /// <param name="voiceName">Name for the new voice.</param>
    /// <param name="audioFilePath">Path to the audio reference file.</param>
    /// <param name="transcription">Optional transcription text for the audio.</param>
    /// <param name="overwrite">Whether to overwrite an existing voice with the same name.</param>
    /// <param name="disableTranscription">Whether to skip automatic transcription.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (success, message) indicating the result.</returns>
    Task<(bool Success, string Message)> CreateVoiceAsync(
        string voiceName, string audioFilePath, string? transcription = null,
        bool overwrite = false, bool disableTranscription = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a voice by name.
    /// </summary>
    /// <returns>Tuple of (success, message) indicating the result.</returns>
    Task<(bool Success, string Message)> DeleteVoiceAsync(string voiceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a voice from <paramref name="currentName"/> to <paramref name="newName"/>.
    /// </summary>
    /// <returns>Tuple of (success, message) indicating the result.</returns>
    Task<(bool Success, string Message)> RenameVoiceAsync(string currentName, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the prompt cache for a specific voice.
    /// Removes cached .pkl files for the voice from both memory and disk.
    /// </summary>
    /// <param name="voiceName">Name of the voice to clear cache for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (success, message) indicating the result.</returns>
    Task<(bool Success, string Message)> ClearVoiceCacheAsync(string voiceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the prompt cache for all voices.
    /// Removes all cached .pkl files from both memory and disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (success, message) indicating the result.</returns>
    Task<(bool Success, string Message)> ClearAllVoiceCacheAsync(CancellationToken cancellationToken = default);
}
