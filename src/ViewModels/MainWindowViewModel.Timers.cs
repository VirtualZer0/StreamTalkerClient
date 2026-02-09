using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure;

namespace StreamTalkerClient.ViewModels;

/// <summary>
/// Timer setup and callback methods for MainWindowViewModel.
/// Manages the UI update timer (250ms), auto-save timer (30s),
/// and GPU polling timer (5s / 30s adaptive).
/// </summary>
public partial class MainWindowViewModel
{
    // ═══════════════════════════════════════════════════════════
    //  TIMER SETUP
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Creates and starts all periodic timers: UI update, auto-save, and GPU polling.
    /// </summary>
    private void StartTimers()
    {
        // UI update timer (queue display, cache stats)
        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(AppConstants.Intervals.UiUpdateMs)
        };
        _uiUpdateTimer.Tick += OnUiUpdateTick;
        _uiUpdateTimer.Start();

        // Auto-save timer (every 30 seconds)
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _autoSaveTimer.Tick += OnAutoSaveTick;
        _autoSaveTimer.Start();

        // GPU update timer (every 5 seconds)
        _gpuUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _gpuUpdateTimer.Tick += OnGpuUpdateTick;
        _gpuUpdateTimer.Start();
    }

    // ═══════════════════════════════════════════════════════════
    //  TIMER CALLBACKS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// UI update timer callback. Refreshes queue display and cache stats.
    /// </summary>
    private void OnUiUpdateTick(object? sender, EventArgs e)
    {
        UpdateQueueDisplay();
        UpdateCacheDisplay();
    }

    /// <summary>
    /// Auto-save timer callback. Persists current settings to disk.
    /// </summary>
    private void OnAutoSaveTick(object? sender, EventArgs e)
    {
        SaveSettings();
    }

    /// <summary>
    /// GPU update timer callback. Polls the TTS server for current GPU usage.
    /// Adapts polling frequency based on server availability (5s when available, 30s when not).
    /// </summary>
    private void OnGpuUpdateTick(object? sender, EventArgs e)
    {
        if (!IsServerAvailable)
        {
            // Server unavailable - reduce frequency to save CPU
            UpdateGpuTimerInterval(isServerAvailable: false);
            return;
        }

        // Server available - ensure normal frequency
        UpdateGpuTimerInterval(isServerAvailable: true);

        _ = Task.Run(async () =>
        {
            try
            {
                var gpuUsage = await _ttsClient.GetGpuUsageAsync();
                if (gpuUsage != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        UpdateGpuDisplay(gpuUsage);
                        CheckVramThresholds();
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "GPU update tick failed");
            }
        });
    }

    /// <summary>
    /// Adjusts the GPU timer interval based on server availability.
    /// Uses 5s when the server is available, 30s when unavailable.
    /// </summary>
    /// <param name="isServerAvailable">Whether the TTS server is currently reachable.</param>
    private void UpdateGpuTimerInterval(bool isServerAvailable)
    {
        if (_gpuUpdateTimer == null)
            return;

        var desiredInterval = isServerAvailable ? 5 : 30;  // 5s when available, 30s when unavailable
        var currentInterval = (int)_gpuUpdateTimer.Interval.TotalSeconds;

        if (currentInterval != desiredInterval)
        {
            _gpuUpdateTimer.Interval = TimeSpan.FromSeconds(desiredInterval);
            _logger.LogDebug("GPU timer interval changed to {Interval}s (server available: {Available})",
                desiredInterval, isServerAvailable);
        }
    }
}
