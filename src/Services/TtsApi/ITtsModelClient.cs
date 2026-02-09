namespace StreamTalkerClient.Services.TtsApi;

/// <summary>
/// Interface for TTS model lifecycle operations: load, unload, status queries, and auto-unload configuration.
/// </summary>
public interface ITtsModelClient
{
    /// <summary>
    /// Loads a TTS model with the specified configuration options.
    /// </summary>
    /// <param name="modelId">Model identifier (e.g. "0.6B", "1.7B").</param>
    /// <param name="attention">Attention implementation (e.g. "auto", "flash_attention_2").</param>
    /// <param name="quantization">Quantization mode (e.g. "none", "int8").</param>
    /// <param name="warmup">Warmup mode (e.g. "none", "basic").</param>
    /// <param name="warmupLang">Language to use for warmup inference.</param>
    /// <param name="warmupVoice">Voice to use for warmup inference.</param>
    /// <param name="warmupTimeout">Timeout in seconds for warmup.</param>
    /// <param name="enableOptimizations">Whether to enable inference optimizations.</param>
    /// <param name="torchCompile">Whether to enable torch.compile.</param>
    /// <param name="cudaGraphs">Whether to enable CUDA graphs.</param>
    /// <param name="compileCodebook">Whether to compile the codebook model.</param>
    /// <param name="fastCodebook">Whether to use fast codebook mode.</param>
    /// <param name="forceCpu">Whether to force CPU execution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the model was loaded successfully.</returns>
    Task<bool> LoadModelAsync(
        string modelId,
        string attention = "auto", string quantization = "none", string warmup = "none",
        string? warmupLang = null, string? warmupVoice = null, int? warmupTimeout = null,
        bool enableOptimizations = true, bool torchCompile = true, bool cudaGraphs = false,
        bool compileCodebook = true, bool fastCodebook = true, bool forceCpu = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads a currently loaded model to free GPU memory.
    /// </summary>
    Task<bool> UnloadModelAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures automatic model unloading after a period of inactivity.
    /// </summary>
    /// <param name="modelId">Model identifier.</param>
    /// <param name="minutes">Minutes of inactivity before auto-unload (0 = disabled).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> SetAutoUnloadAsync(string modelId, int minutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of all models (loaded, loading, unloaded, etc.).
    /// </summary>
    Task<ModelsStatusResponse?> GetModelsStatusAsync(CancellationToken cancellationToken = default);
}
