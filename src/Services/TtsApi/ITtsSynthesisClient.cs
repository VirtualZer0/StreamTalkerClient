namespace StreamTalkerClient.Services.TtsApi;

/// <summary>
/// Interface for TTS speech synthesis operations: batch/single synthesis, skip, and health check.
/// </summary>
public interface ITtsSynthesisClient
{
    /// <summary>
    /// Synthesizes a batch of texts into WAV audio files using the specified voice.
    /// Returns a dictionary mapping text index to WAV byte data, or null on failure.
    /// </summary>
    Task<Dictionary<int, byte[]>?> SynthesizeBatchAsync(
        string[] texts, string voice,
        string model = "1.7B", string language = "Auto", bool doSample = true,
        double? speed = null, double? temperature = null,
        int? maxNewTokens = null, double? repetitionPenalty = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synthesizes a single text into WAV audio data, or null on failure.
    /// </summary>
    Task<byte[]?> SynthesizeSingleAsync(
        string text, string voice,
        string model = "1.7B", string language = "Auto", bool doSample = true,
        double? speed = null, double? temperature = null,
        int? maxNewTokens = null, double? repetitionPenalty = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests the TTS server to abort the currently running inference.
    /// </summary>
    Task<bool> SkipInferenceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the TTS server is healthy and responding.
    /// </summary>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
