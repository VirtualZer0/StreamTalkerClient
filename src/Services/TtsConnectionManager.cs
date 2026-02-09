using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Services;

/// <summary>
/// Manages TTS server connection state and handles reconnection with automatic data reloading.
/// Provides events for consumers to react to connection state changes.
/// </summary>
public class TtsConnectionManager : IDisposable
{
    private readonly ILogger<TtsConnectionManager> _logger;
    private readonly QwenTtsClient _ttsClient;
    private readonly DebouncedTimer _healthCheckTimer;

    private bool _serverAvailable;
    private bool _disposed;

    /// <summary>
    /// Whether the TTS server is currently available.
    /// </summary>
    public bool IsServerAvailable => _serverAvailable;

    /// <summary>
    /// Raised when server availability changes (true = available, false = unavailable).
    /// </summary>
    public event EventHandler<bool>? ServerAvailabilityChanged;

    /// <summary>
    /// Raised when the server connection is lost.
    /// </summary>
    public event EventHandler? ServerConnectionLost;

    /// <summary>
    /// Raised when the server connection is restored after being lost.
    /// </summary>
    public event EventHandler? ServerReconnected;

    /// <summary>
    /// Raised when voices are successfully reloaded after reconnection.
    /// </summary>
    public event EventHandler<List<VoiceInfo>>? VoicesReloaded;

    /// <summary>
    /// Raised when models status is successfully reloaded after reconnection.
    /// </summary>
    public event EventHandler<ModelsStatusResponse>? ModelsReloaded;

    /// <summary>
    /// Raised when GPU usage is successfully retrieved.
    /// </summary>
    public event EventHandler<GpuUsageResponse>? GpuUsageUpdated;

    public TtsConnectionManager(QwenTtsClient ttsClient, TimeSpan healthCheckInterval)
    {
        _logger = AppLoggerFactory.CreateLogger<TtsConnectionManager>();
        _ttsClient = ttsClient ?? throw new ArgumentNullException(nameof(ttsClient));

        _healthCheckTimer = new DebouncedTimer(
            CheckHealthInternalAsync,
            healthCheckInterval,
            "TtsHealthCheck",
            _logger);
    }

    /// <summary>
    /// Starts the health check timer.
    /// </summary>
    public void Start()
    {
        _healthCheckTimer.Start();
        _logger.LogInformation("TTS connection manager started");
    }

    /// <summary>
    /// Stops the health check timer.
    /// </summary>
    public void Stop()
    {
        _healthCheckTimer.Stop();
        _logger.LogInformation("TTS connection manager stopped");
    }

    /// <summary>
    /// Triggers an immediate health check.
    /// </summary>
    public async Task<bool> CheckHealthNowAsync()
    {
        return await _healthCheckTimer.TriggerNowAsync();
    }

    /// <summary>
    /// Internal health check implementation.
    /// Also polls model status when server is available to detect state changes like warming_up.
    /// </summary>
    private async Task CheckHealthInternalAsync()
    {
        var wasAvailable = _serverAvailable;

        try
        {
            _serverAvailable = await _ttsClient.CheckHealthAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Server health check failed");
            _serverAvailable = false;
        }

        // Notify if availability changed
        if (wasAvailable != _serverAvailable)
        {
            _logger.LogInformation("TTS server availability changed: {Available}", _serverAvailable);
            ServerAvailabilityChanged?.Invoke(this, _serverAvailable);

            if (_serverAvailable)
            {
                // Server came back online - reload data
                _logger.LogInformation("TTS server reconnected, reloading data...");
                ServerReconnected?.Invoke(this, EventArgs.Empty);
                await ReloadServerDataAsync();
            }
            else
            {
                // Server went offline
                _logger.LogWarning("TTS server connection lost");
                ServerConnectionLost?.Invoke(this, EventArgs.Empty);
            }
        }
        // If server is available, also poll model status to detect warming_up and other transitions
        else if (_serverAvailable)
        {
            try
            {
                var modelStatus = await _ttsClient.GetModelsStatusAsync();
                if (modelStatus != null)
                {
                    ModelsReloaded?.Invoke(this, modelStatus);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to poll model status during health check");
            }
        }
    }

    /// <summary>
    /// Reloads all server data (voices, models, GPU usage) with retry logic.
    /// Called automatically on reconnection.
    /// </summary>
    private async Task ReloadServerDataAsync()
    {
        // Run voice and model reload in parallel for faster recovery
        var voicesTask = ReloadVoicesWithRetryAsync();
        var modelsTask = ReloadModelsWithRetryAsync();

        await Task.WhenAll(voicesTask, modelsTask);

        // Notify consumers of reloaded data
        var voices = voicesTask.Result;
        var models = modelsTask.Result;

        if (voices != null && voices.Count > 0)
        {
            _logger.LogInformation("Reloaded {Count} voices from server", voices.Count);
            VoicesReloaded?.Invoke(this, voices);
        }

        if (models != null)
        {
            _logger.LogInformation("Reloaded models status from server");
            ModelsReloaded?.Invoke(this, models);
        }

        // Also update GPU usage
        await UpdateGpuUsageAsync();
    }

    /// <summary>
    /// Reloads voices with retry logic and exponential backoff.
    /// </summary>
    private async Task<List<VoiceInfo>?> ReloadVoicesWithRetryAsync(int maxRetries = 3)
    {
        Func<Task<List<VoiceInfo>>> operation = () => _ttsClient.GetVoicesAsync();
        return await operation.ExecuteWithRetryAsync(_logger, "reload voices", maxRetries, baseDelayMs: 500);
    }

    /// <summary>
    /// Reloads models status with retry logic and exponential backoff.
    /// </summary>
    private async Task<ModelsStatusResponse?> ReloadModelsWithRetryAsync(int maxRetries = 3)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var result = await _ttsClient.GetModelsStatusAsync();
                if (result != null)
                    return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries - 1)
                {
                    _logger.LogError(ex, "Failed to reload models after {Attempts} attempts", maxRetries);
                    return null;
                }

                var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt));
                _logger.LogWarning(ex, "Failed to reload models, attempt {Attempt}/{Max}. Retrying in {Delay}ms",
                    attempt + 1, maxRetries, delay.TotalMilliseconds);
                await Task.Delay(delay);
            }
        }
        return null;
    }

    /// <summary>
    /// Updates GPU usage and notifies consumers.
    /// </summary>
    public async Task UpdateGpuUsageAsync()
    {
        if (!_serverAvailable)
            return;

        try
        {
            var gpuUsage = await _ttsClient.GetGpuUsageAsync();
            if (gpuUsage != null)
            {
                GpuUsageUpdated?.Invoke(this, gpuUsage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get GPU usage");
        }
    }

    /// <summary>
    /// Manually triggers a reload of all server data.
    /// Use this when you need to refresh data outside of the automatic reconnection flow.
    /// </summary>
    public async Task ReloadAllDataAsync()
    {
        if (!_serverAvailable)
        {
            _logger.LogWarning("Cannot reload data: server not available");
            return;
        }

        await ReloadServerDataAsync();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _healthCheckTimer.Dispose();

        // Clear event handlers
        ServerAvailabilityChanged = null;
        ServerConnectionLost = null;
        ServerReconnected = null;
        VoicesReloaded = null;
        ModelsReloaded = null;
        GpuUsageUpdated = null;
    }
}
