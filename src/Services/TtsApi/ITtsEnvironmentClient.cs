namespace StreamTalkerClient.Services.TtsApi;

/// <summary>
/// Interface for TTS server environment queries: GPU usage, VRAM limits, and inference timeout configuration.
/// </summary>
public interface ITtsEnvironmentClient
{
    /// <summary>
    /// Gets current GPU memory usage (total and used in MB).
    /// </summary>
    Task<GpuUsageResponse?> GetGpuUsageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the configured maximum VRAM limit and total VRAM available.
    /// </summary>
    Task<MaxVramResponse?> GetMaxVramAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the maximum VRAM the TTS server is allowed to use.
    /// </summary>
    /// <param name="maxVramMb">Maximum VRAM in megabytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SetMaxVramResponse?> SetMaxVramAsync(int maxVramMb, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current inference timeout setting in seconds.
    /// </summary>
    Task<int?> GetInferenceTimeoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the inference timeout in seconds.
    /// </summary>
    Task<bool> SetInferenceTimeoutAsync(int timeoutSeconds, CancellationToken cancellationToken = default);
}
