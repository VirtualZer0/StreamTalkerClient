using Avalonia;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;

namespace StreamTalkerClient.ViewModels;

/// <summary>
/// Status display and UI update helper methods for MainWindowViewModel.
/// Includes all UpdateXxxDisplay/UpdateXxxStatus methods, GPU display,
/// VRAM helpers, indicator state, and the GetLocalizedString helper.
/// </summary>
public partial class MainWindowViewModel
{
    // ═══════════════════════════════════════════════════════════
    //  LOCALIZATION HELPER
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Retrieves a localized string from the application resource dictionaries.
    /// Returns the fallback value if the key is not found.
    /// </summary>
    /// <param name="key">The resource key to look up.</param>
    /// <param name="fallback">Fallback string if the key is not found.</param>
    /// <returns>The localized string or the fallback value.</returns>
    private static string GetLocalizedString(string key, string fallback = "")
    {
        if (Application.Current != null &&
            Application.Current.TryGetResource(key, Application.Current.ActualThemeVariant, out var resource) &&
            resource is string str)
        {
            return str;
        }
        return fallback;
    }

    /// <summary>
    /// Refreshes all platform and server status displays using current state.
    /// Typically called after a language change to update localized strings.
    /// </summary>
    private void RefreshAllStatusDisplays()
    {
        UpdateTwitchStatusDisplay(TwitchConnectionState);
        UpdateVkStatusDisplay(VkConnectionState);
        UpdateServerStatus(IsServerAvailable);
        UpdateModelStatusDisplay(_lastModelsStatus);
    }

    // ═══════════════════════════════════════════════════════════
    //  QUEUE & CACHE DISPLAY
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Updates the queue items collection and group header text
    /// from the current queue state.
    /// </summary>
    private void UpdateQueueDisplay()
    {
        try
        {
            var messages = _queueManager.GetAllMessages();
            var count = messages.Count;

            // Build a fingerprint to detect changes and skip unnecessary rebuilds
            var displayCount = Math.Min(count, AppConstants.Limits.MaxQueueDisplayItems);
            var fpBuilder = new System.Text.StringBuilder(displayCount * 12);
            fpBuilder.Append(count);
            for (int i = 0; i < displayCount; i++)
            {
                fpBuilder.Append('|');
                fpBuilder.Append((int)messages[i].State);
            }
            var fingerprint = fpBuilder.ToString();

            if (fingerprint == _lastQueueFingerprint)
                return;

            _lastQueueFingerprint = fingerprint;

            QueueGroupText = string.Format(GetLocalizedString("QueueCountFormat", "Queue ({0})"), count);

            QueueItems.Clear();
            for (int i = 0; i < displayCount; i++)
            {
                var msg = messages[i];
                QueueItems.Add(new QueueDisplayItem
                {
                    StateEmoji = msg.GetStateIcon(),
                    StateName = msg.State.ToString(),
                    VoiceName = msg.VoiceName,
                    Username = msg.Username,
                    TextContent = msg.GetDisplayText(),
                    State = msg.State
                });
            }

            if (count > AppConstants.Limits.MaxQueueDisplayItems)
            {
                QueueItems.Add(new QueueDisplayItem
                {
                    TextContent = string.Format(GetLocalizedString("QueueMoreFormat", "... and {0} more"), count - AppConstants.Limits.MaxQueueDisplayItems),
                    IsOverflowIndicator = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateQueueDisplay");
        }
    }

    /// <summary>
    /// Updates the cache size text and progress bar from the current cache state.
    /// Skips updates if the cache stats have not changed since the last check.
    /// </summary>
    private void UpdateCacheDisplay()
    {
        var currentSize = _cacheManager.CurrentSizeBytes;
        var currentCount = _cacheManager.ItemCount;

        // Early exit if cache stats haven't changed
        if (currentSize == _lastCacheSizeBytes && currentCount == _lastCacheItemCount)
            return;

        _lastCacheSizeBytes = currentSize;
        _lastCacheItemCount = currentCount;

        var sizeMb = currentSize / (1024.0 * 1024.0);
        var limitMb = _cacheManager.CacheLimitBytes / (1024.0 * 1024.0);
        CacheSizeText = string.Format(GetLocalizedString("CacheSizeDetailFormat", "{0:F1} / {1:F0} MB ({2} items)"), sizeMb, limitMb, currentCount);
        CacheProgress = (int)_cacheManager.GetUsagePercent();
    }

    // ═══════════════════════════════════════════════════════════
    //  PLATFORM STATUS DISPLAY
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Updates the status display for Twitch platform.
    /// Delegates to the unified <see cref="UpdatePlatformStatusDisplay"/> method.
    /// </summary>
    private void UpdateTwitchStatusDisplay(StreamConnectionState state) =>
        UpdatePlatformStatusDisplay(state,
            "TwitchTab", "Twitch", Channel,
            "JoiningChannelStatus", "Connected, joining channel...",
            s => TwitchConnectionState = s,
            s => TwitchStatusText = s,
            s => TwitchConnectButtonText = s,
            s => TwitchTabHeader = s,
            s => TwitchTabTooltip = s);

    /// <summary>
    /// Updates the status display for VK Play platform.
    /// Delegates to the unified <see cref="UpdatePlatformStatusDisplay"/> method.
    /// </summary>
    private void UpdateVkStatusDisplay(StreamConnectionState state) =>
        UpdatePlatformStatusDisplay(state,
            "VKPlayTab", "VK Play", VkChannel,
            "SubscribingStatus", "Connected, subscribing...",
            s => VkConnectionState = s,
            s => VkStatusText = s,
            s => VkConnectButtonText = s,
            s => VkTabHeader = s,
            s => VkTabTooltip = s);

    /// <summary>
    /// Unified platform status display logic. Resolves localized strings for each connection state
    /// and applies them via the provided setter delegates.
    /// </summary>
    /// <param name="state">Current connection state.</param>
    /// <param name="tabKey">Resource key for the platform tab name.</param>
    /// <param name="tabDefault">Fallback tab name if resource not found.</param>
    /// <param name="channel">Channel name for the "Joined" state display.</param>
    /// <param name="connectedKey">Resource key for the "Connected" state (platform-specific).</param>
    /// <param name="connectedDefault">Fallback for connected status text.</param>
    /// <param name="setState">Setter for the connection state property.</param>
    /// <param name="setStatus">Setter for the status text property.</param>
    /// <param name="setButton">Setter for the connect/disconnect button text property.</param>
    /// <param name="setHeader">Setter for the tab header property.</param>
    /// <param name="setTooltip">Setter for the tab tooltip property.</param>
    private void UpdatePlatformStatusDisplay(
        StreamConnectionState state,
        string tabKey, string tabDefault,
        string channel,
        string connectedKey, string connectedDefault,
        Action<StreamConnectionState> setState,
        Action<string> setStatus,
        Action<string> setButton,
        Action<string> setHeader,
        Action<string> setTooltip)
    {
        setState(state);
        var tab = GetLocalizedString(tabKey, tabDefault);

        var (statusText, buttonText) = state switch
        {
            StreamConnectionState.Disconnected => (
                GetLocalizedString("DisconnectedStatus", "Disconnected"),
                GetLocalizedString("ConnectButton", "Connect")),
            StreamConnectionState.Connecting => (
                GetLocalizedString("ConnectingStatus", "Connecting..."),
                GetLocalizedString("DisconnectButton", "Disconnect")),
            StreamConnectionState.Connected => (
                GetLocalizedString(connectedKey, connectedDefault),
                GetLocalizedString("DisconnectButton", "Disconnect")),
            StreamConnectionState.Joined => (
                string.Format(GetLocalizedString("JoinedFormat", "Joined: {0}"), channel),
                GetLocalizedString("DisconnectButton", "Disconnect")),
            StreamConnectionState.Error => (
                GetLocalizedString("ConnectionError", "Connection error"),
                GetLocalizedString("ReconnectButton", "Reconnect")),
            _ => (
                GetLocalizedString("UnknownStatus", "Unknown"),
                GetLocalizedString("ConnectAction", "Connect"))
        };

        setStatus(statusText);
        setButton(buttonText);
        setHeader(tab);
        setTooltip($"{tab} — {statusText}");
    }

    // ═══════════════════════════════════════════════════════════
    //  SERVER & MODEL STATUS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Updates the TTS server availability status display.
    /// </summary>
    /// <param name="available">Whether the TTS server is currently reachable.</param>
    private void UpdateServerStatus(bool available)
    {
        IsServerAvailable = available;
        if (available)
        {
            ServerStatusText = GetLocalizedString("ConnectedStatus", "Connected");
            ServerState = "available";
        }
        else
        {
            ServerStatusText = GetLocalizedString("ServerUnavailable", "Unavailable");
            ServerState = "unavailable";
        }
    }

    /// <summary>
    /// Updates the models list and selection from a server status response.
    /// Suppresses model switch to avoid reloading a model that was just loaded.
    /// </summary>
    private void UpdateModelsFromStatus(ModelsStatusResponse status)
    {
        if (status.Models == null)
            return;

        // Update models list
        var modelKeys = status.Models.Keys.OrderBy(k => k).ToList();
        Models.Clear();
        foreach (var key in modelKeys)
        {
            Models.Add(key);
        }

        // Re-select current model (reset first to force PropertyChanged notification)
        // Suppress model switch to avoid re-loading a model that was just loaded
        var targetModel = !string.IsNullOrEmpty(_settings.Model.Core.Name) && Models.Contains(_settings.Model.Core.Name)
            ? _settings.Model.Core.Name
            : Models.Count > 0 ? Models[0] : null;
        _suppressModelSwitch = true;
        SelectedModel = null;
        SelectedModel = targetModel;
        _suppressModelSwitch = false;

        // Update model status display
        UpdateModelStatusDisplay(status);
    }

    /// <summary>
    /// Updates the model status text, state, and indicator based on the
    /// server-reported model status (loading, warming_up, ready, unloaded).
    /// </summary>
    private void UpdateModelStatusDisplay(ModelsStatusResponse? status)
    {
        _lastModelsStatus = status;

        if (status?.Models == null || string.IsNullOrEmpty(SelectedModel))
        {
            ModelStatusText = GetLocalizedString("ModelUnknown", "Unknown");
            ModelState = "unknown";
            _rawModelServerStatus = "unloaded";
            UpdateIndicatorState();
            OnPropertyChanged(nameof(IsModelLoaded));
            return;
        }

        if (status.Models.TryGetValue(SelectedModel, out var info))
        {
            _rawModelServerStatus = info.Status ?? "unloaded";

            // Sync auto-unload state from server (0 = disabled, >0 = enabled with minutes)
            var serverAutoUnload = info.AutoUnloadMinutes > 0;

            if (AutoUnload != serverAutoUnload || (serverAutoUnload && AutoUnloadMinutes != info.AutoUnloadMinutes))
            {
                var wasInitialized = _isInitialized;
                _isInitialized = false;
                AutoUnload = serverAutoUnload;
                if (serverAutoUnload)
                    AutoUnloadMinutes = info.AutoUnloadMinutes;
                _isInitialized = wasInitialized;

                _settings.Model.AutoUnload.Enabled = serverAutoUnload;
                if (serverAutoUnload)
                    _settings.Model.AutoUnload.Minutes = info.AutoUnloadMinutes;
            }

            switch (info.Status)
            {
                case "loading":
                    ModelStatusText = GetLocalizedString("ModelLoading", "Loading...");
                    ModelState = "loading";
                    IsModelOperationInProgress = true;
                    break;
                case "warming_up":
                    ModelStatusText = GetLocalizedString("ModelWarmingUp", "Warming up...");
                    ModelState = "warming_up";
                    IsModelOperationInProgress = true;
                    break;
                case "ready":
                    ModelStatusText = GetLocalizedString("ModelLoaded", "Loaded");
                    ModelState = "loaded";
                    IsModelOperationInProgress = false;
                    break;
                case "unloaded":
                default:
                    ModelStatusText = GetLocalizedString("ModelUnloaded", "Unloaded");
                    ModelState = "unloaded";
                    IsModelOperationInProgress = false;
                    break;
            }
        }
        else
        {
            ModelStatusText = GetLocalizedString("ModelNotFound", "Not found");
            ModelState = "error";
            _rawModelServerStatus = "error";
        }

        UpdateIndicatorState();
        OnPropertyChanged(nameof(IsModelLoaded));
    }

    // ═══════════════════════════════════════════════════════════
    //  GPU DISPLAY
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Updates GPU usage display properties from a server GPU usage response.
    /// Only updates properties whose values have actually changed to minimize
    /// PropertyChanged notifications.
    /// </summary>
    private void UpdateGpuDisplay(GpuUsageResponse gpuUsage)
    {
        var usedMb = gpuUsage.UsedMb;
        var totalMb = gpuUsage.TotalMb;
        var percent = totalMb > 0 ? (int)(usedMb / totalMb * 100) : 0;

        // Only update properties if values changed (reduces PropertyChanged notifications)
        if (GpuUsagePercent != percent)
            GpuUsagePercent = percent;

        var usageText = string.Format(GetLocalizedString("GpuUsageFormat", "GPU: {0:F0} / {1:F0} MB ({2}%)"), usedMb, totalMb, percent);
        if (GpuUsageText != usageText)
            GpuUsageText = usageText;

        if (Math.Abs(GpuUsedMb - usedMb) > 0.1)
            GpuUsedMb = usedMb;

        if (Math.Abs(GpuTotalMb - totalMb) > 0.1)
            GpuTotalMb = totalMb;

        // Keep slider max in sync with actual GPU total
        var totalMbInt = (int)totalMb;
        if (totalMbInt > 0 && MaxVramSliderMax != totalMbInt)
        {
            MaxVramSliderMax = totalMbInt;
            UpdateVramLimit();
        }

        UpdateVramTooltip();
    }

    /// <summary>
    /// Updates the hotkey display text from the current hotkey configuration.
    /// </summary>
    private void UpdateHotkeysText()
    {
        var skipCurrent = GlobalHotkeyService.GetKeyDisplayName(_hotkeyService.SkipCurrentKey);
        var skipAll = GlobalHotkeyService.GetKeyDisplayName(_hotkeyService.SkipAllKey);
        HotkeysText = $"Skip: {skipCurrent} | Clear: {skipAll}";
    }

    /// <summary>
    /// Updates the VRAM tooltip text with detailed usage information.
    /// </summary>
    private void UpdateVramTooltip()
    {
        if (GpuTotalMb <= 0)
        {
            VramTooltipText = "";
            return;
        }

        var usedGb = GpuUsedMb / 1024.0;
        var totalGb = GpuTotalMb / 1024.0;
        var percent = (int)(GpuUsedMb / GpuTotalMb * 100);
        VramTooltipText = $"VRAM: {usedGb:F2} GB / {totalGb:F0} GB ({percent}%)";
    }

    /// <summary>
    /// Recalculates the VRAM limit percentage and whether a limit is active.
    /// </summary>
    private void UpdateVramLimit()
    {
        if (MaxVramSliderMax > 0)
        {
            VramLimitPercent = (double)MaxVramSliderValue / MaxVramSliderMax * 100.0;
            HasVramLimit = MaxVramSliderValue < MaxVramSliderMax;
        }
        else
        {
            VramLimitPercent = 100;
            HasVramLimit = false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  NOTIFICATIONS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Shows a notification to the user. Thread-safe (marshals to UI thread).
    /// </summary>
    /// <param name="message">Notification message text.</param>
    /// <param name="severity">Severity level (Info, Success, Warning, Danger).</param>
    /// <param name="durationMs">Auto-dismiss delay in ms. 0 = persistent (manual dismiss only).</param>
    /// <param name="closeable">Whether the user can dismiss the notification.</param>
    /// <returns>The notification ID for later reference.</returns>
    public long ShowNotification(string message, NotificationSeverity severity,
        int durationMs = 5000, bool closeable = true)
    {
        var item = new NotificationItem
        {
            Message = message,
            Severity = severity,
            DurationMs = durationMs,
            IsCloseable = closeable
        };

        Dispatcher.UIThread.Post(() =>
        {
            Notifications.Add(item);

            if (durationMs > 0)
            {
                _ = AutoDismissNotificationInternalAsync(item.Id, durationMs);
            }
        });

        return item.Id;
    }

    /// <summary>
    /// Dismisses a notification with fade-out animation.
    /// </summary>
    internal async Task DismissNotificationInternalAsync(long notificationId)
    {
        var item = Notifications.FirstOrDefault(n => n.Id == notificationId);
        if (item == null) return;

        item.IsRemoving = true;
        await Task.Delay(300);
        Notifications.Remove(item);
    }

    /// <summary>
    /// Auto-dismisses a notification after the specified delay.
    /// </summary>
    private async Task AutoDismissNotificationInternalAsync(long notificationId, int delayMs)
    {
        await Task.Delay(delayMs);
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await DismissNotificationInternalAsync(notificationId);
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  VRAM THRESHOLD CHECKS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Checks VRAM usage against thresholds and shows/clears notifications.
    /// Called after GPU display update in the GPU timer.
    /// </summary>
    private void CheckVramThresholds()
    {
        // Check 1: VRAM limit exceeded (MaxVramSliderValue > 0 AND GpuUsedMb > MaxVramSliderValue)
        if (MaxVramSliderValue > 0 && GpuUsedMb > MaxVramSliderValue)
        {
            if (!_vramLimitExceededNotified)
            {
                _vramLimitExceededNotified = true;
                var message = GetLocalizedString("VramLimitExceededNotification",
                    "VRAM usage exceeds the configured limit! Generated results may be cut off.");
                _vramLimitExceededNotificationId = ShowNotification(message, NotificationSeverity.Danger,
                    durationMs: 0, closeable: true);
            }
        }
        else
        {
            if (_vramLimitExceededNotified)
            {
                _vramLimitExceededNotified = false;
                if (_vramLimitExceededNotificationId > 0)
                {
                    _ = Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await DismissNotificationInternalAsync(_vramLimitExceededNotificationId);
                    });
                    _vramLimitExceededNotificationId = 0;
                }
            }
        }

        // Check 2: VRAM >70% of total
        if (GpuTotalMb > 0 && GpuUsedMb / GpuTotalMb > 0.70)
        {
            if (!_vramHighUsageNotified)
            {
                _vramHighUsageNotified = true;
                var message = GetLocalizedString("VramHighUsageNotification",
                    "VRAM usage is above 70% of total capacity!");
                _vramHighUsageNotificationId = ShowNotification(message, NotificationSeverity.Warning,
                    durationMs: 0, closeable: true);
            }
        }
        else
        {
            if (_vramHighUsageNotified)
            {
                _vramHighUsageNotified = false;
                if (_vramHighUsageNotificationId > 0)
                {
                    _ = Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await DismissNotificationInternalAsync(_vramHighUsageNotificationId);
                    });
                    _vramHighUsageNotificationId = 0;
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  INDICATOR STATE
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Updates the global indicator state based on the current model status
    /// and synthesis activity. The indicator shows: Error, Loading, WarmingUp,
    /// Active (synthesizing), Ready, or Unloaded.
    /// </summary>
    private void UpdateIndicatorState()
    {
        if (ModelState == "error" || _rawModelServerStatus == "error")
        {
            IndicatorState = IndicatorState.Error;
        }
        else if (_rawModelServerStatus == "loading")
        {
            IndicatorState = IndicatorState.Loading;
        }
        else if (_rawModelServerStatus == "warming_up")
        {
            IndicatorState = IndicatorState.WarmingUp;
        }
        else if (_rawModelServerStatus == "ready")
        {
            if (IsSynthesizing)
            {
                IndicatorState = IndicatorState.Active;
                ModelStatusText = GetLocalizedString("ModelSynthesizing", "Synthesizing...");
            }
            else
            {
                IndicatorState = IndicatorState.Ready;
                ModelStatusText = GetLocalizedString("ModelLoaded", "Loaded");
            }
        }
        else
        {
            IndicatorState = IndicatorState.Unloaded;
        }
    }
}
