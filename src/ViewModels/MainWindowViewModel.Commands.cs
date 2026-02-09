using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;

namespace StreamTalkerClient.ViewModels;

/// <summary>
/// Relay commands for MainWindowViewModel.
/// All methods decorated with [RelayCommand] plus their private helper methods
/// (LoadCurrentModelAsync, UnloadCurrentModelAsync, SwitchModelAsync, SkipCurrentAsync).
/// </summary>
public partial class MainWindowViewModel
{
    // ═══════════════════════════════════════════════════════════
    //  COMMANDS - Twitch
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Connects to or disconnects from the configured Twitch channel.
    /// </summary>
    [RelayCommand]
    private async Task ConnectTwitchAsync()
    {
        try
        {
            if (_twitchService.ConnectionState == StreamConnectionState.Joined ||
                _twitchService.ConnectionState == StreamConnectionState.Connected ||
                _twitchService.ConnectionState == StreamConnectionState.Connecting)
            {
                // Disconnect
                await _twitchService.DisconnectAsync();
                StatusBarText = GetLocalizedString("TwitchDisconnected", "Twitch disconnected");
                return;
            }

            if (string.IsNullOrWhiteSpace(Channel))
            {
                StatusBarText = GetLocalizedString("EnterChannelName", "Enter channel name");
                return;
            }

            StatusBarText = string.Format(GetLocalizedString("ConnectingToFormat", "Connecting to {0} channel: {1}..."), "Twitch", Channel);
            await _twitchService.JoinChannelAsync(Channel.Trim().ToLowerInvariant());
            SaveSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Twitch");
            StatusBarText = string.Format(GetLocalizedString("ConnectionErrorFormat", "{0} connection error: {1}"), "Twitch", ex.Message);
        }
    }

    /// <summary>
    /// Connects to or disconnects from the configured VK Play channel.
    /// </summary>
    [RelayCommand]
    private async Task ConnectVkPlayAsync()
    {
        try
        {
            if (_vkPlayService.ConnectionState == StreamConnectionState.Joined ||
                _vkPlayService.ConnectionState == StreamConnectionState.Connected ||
                _vkPlayService.ConnectionState == StreamConnectionState.Connecting)
            {
                await _vkPlayService.DisconnectAsync();
                StatusBarText = GetLocalizedString("VKPlayDisconnected", "VK Play disconnected");
                return;
            }

            if (string.IsNullOrWhiteSpace(VkChannel))
            {
                StatusBarText = GetLocalizedString("EnterVKChannelName", "Enter VK Play channel name");
                return;
            }

            StatusBarText = string.Format(GetLocalizedString("ConnectingToFormat", "Connecting to {0} channel: {1}..."), "VK Play", VkChannel);
            await _vkPlayService.JoinChannelAsync(VkChannel.Trim());
            SaveSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to VK Play");
            StatusBarText = string.Format(GetLocalizedString("ConnectionErrorFormat", "{0} connection error: {1}"), "VK Play", ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMANDS - Rewards
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Fetches available Twitch channel point rewards for the configured channel.
    /// </summary>
    [RelayCommand]
    private async Task RefreshRewardsAsync()
    {
        if (string.IsNullOrWhiteSpace(Channel))
        {
            StatusBarText = GetLocalizedString("EnterChannelName", "Enter channel name");
            return;
        }

        try
        {
            StatusBarText = string.Format(GetLocalizedString("LoadingRewardsFormat", "Loading {0} rewards..."), "Twitch");
            await _twitchService.LoadRewardsAsync(Channel.Trim().ToLowerInvariant());

            var customRewards = _twitchService.CustomRewards;
            _settings.Services.TwitchRewardsCache = customRewards;

            Rewards.Clear();
            Rewards.Add(GetLocalizedString("ReadAllMessagesOption", "(Read all messages)"));
            foreach (var reward in customRewards)
            {
                Rewards.Add(reward.title ?? GetLocalizedString("UnnamedReward", "(unnamed)"));
            }

            SelectTwitchRewardById(_settings.Services.Twitch.RewardId);
            StatusBarText = string.Format(GetLocalizedString("LoadedRewardsFormat", "Loaded {0} {1} rewards"), customRewards.Length, "Twitch");
            SaveSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh Twitch rewards");
            StatusBarText = string.Format(GetLocalizedString("FailedLoadRewardsFormat", "Failed to load rewards: {0}"), ex.Message);
        }
    }

    /// <summary>
    /// Fetches available VK Play rewards for the configured channel.
    /// </summary>
    [RelayCommand]
    private async Task RefreshVkRewardsAsync()
    {
        if (string.IsNullOrWhiteSpace(VkChannel))
        {
            StatusBarText = GetLocalizedString("EnterVKChannelName", "Enter VK Play channel name");
            return;
        }

        try
        {
            StatusBarText = string.Format(GetLocalizedString("LoadingRewardsFormat", "Loading {0} rewards..."), "VK Play");
            await _vkPlayService.LoadRewardsAsync(VkChannel.Trim());

            var customRewards = _vkPlayService.CustomRewards;
            _settings.Services.VKRewardsCache = customRewards;

            VkRewards.Clear();
            VkRewards.Add(GetLocalizedString("ReadAllMessagesOption", "(Read all messages)"));
            foreach (var reward in customRewards)
            {
                VkRewards.Add(reward.Title ?? GetLocalizedString("UnnamedReward", "(unnamed)"));
            }

            SelectVkRewardById(_settings.Services.VKPlay.RewardId);
            StatusBarText = string.Format(GetLocalizedString("LoadedRewardsFormat", "Loaded {0} {1} rewards"), customRewards.Length, "VK Play");
            SaveSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh VK Play rewards");
            StatusBarText = string.Format(GetLocalizedString("FailedLoadRewardsFormat", "Failed to load rewards: {0}"), ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMANDS - Voice
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Refreshes the list of available TTS voices from the server.
    /// </summary>
    [RelayCommand]
    private async Task RefreshVoicesAsync()
    {
        if (!IsServerAvailable)
        {
            StatusBarText = GetLocalizedString("TtsServerUnavailable", "TTS server unavailable");
            return;
        }

        try
        {
            StatusBarText = GetLocalizedString("RefreshingVoices", "Refreshing voices...");
            await LoadVoicesFromServerAsync();
            StatusBarText = string.Format(GetLocalizedString("LoadedVoicesFormat", "Loaded {0} voices"), AvailableVoices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh voices");
            StatusBarText = string.Format(GetLocalizedString("FailedRefreshVoicesFormat", "Failed to refresh voices: {0}"), ex.Message);
        }
    }

    /// <summary>
    /// Signal command for the View code-behind to open the ManageVoicesWindow dialog.
    /// </summary>
    [RelayCommand]
    private void ManageVoices()
    {
        // This command is a signal for the View code-behind to open the ManageVoicesWindow.
        // The View will use TtsClient and Settings accessors to pass to the dialog.
        // No-op in the ViewModel: the View subscribes to this command or handles it via code-behind.
        _logger.LogDebug("ManageVoices command invoked - View should open dialog");
    }

    /// <summary>
    /// Signal command for the View code-behind to open the VoiceBindingsWindow dialog.
    /// </summary>
    [RelayCommand]
    private void OpenVoiceBindings()
    {
        // Same pattern as ManageVoices: the View code-behind handles opening the dialog.
        _logger.LogDebug("OpenVoiceBindings command invoked - View should open dialog");
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMANDS - Model
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Loads the currently selected TTS model on the server.
    /// </summary>
    [RelayCommand]
    private async Task LoadModelAsync()
    {
        if (!IsServerAvailable || string.IsNullOrEmpty(SelectedModel))
            return;

        await LoadCurrentModelAsync();
    }

    /// <summary>
    /// Unloads the currently selected TTS model from the server.
    /// </summary>
    [RelayCommand]
    private async Task UnloadModelAsync()
    {
        if (!IsServerAvailable || string.IsNullOrEmpty(SelectedModel))
            return;

        await UnloadCurrentModelAsync();
    }

    /// <summary>
    /// Toggles the model between loaded and unloaded state.
    /// </summary>
    [RelayCommand]
    private async Task TogglePowerAsync()
    {
        if (IsModelOperationInProgress || !IsServerAvailable || string.IsNullOrEmpty(SelectedModel))
            return;

        if (IsModelLoaded)
            await UnloadCurrentModelAsync();
        else
            await LoadCurrentModelAsync();
    }

    /// <summary>
    /// Opens the settings window overlay.
    /// </summary>
    [RelayCommand]
    private void OpenSettingsWindow()
    {
        IsSettingsWindowOpen = true;
    }

    /// <summary>
    /// Loads the currently selected model with all configured parameters
    /// (attention, quantization, warmup, optimizations, etc.).
    /// </summary>
    private async Task LoadCurrentModelAsync()
    {
        if (string.IsNullOrEmpty(SelectedModel))
            return;

        try
        {
            IsModelOperationInProgress = true;
            var attention = SelectedAttention ?? "auto";
            var quantization = SelectedQuantization ?? "none";

            ModelStatusText = GetLocalizedString("ModelLoading", "Loading...");
            ModelState = "loading";
            _rawModelServerStatus = "loading";
            UpdateIndicatorState();
            OnPropertyChanged(nameof(IsModelLoaded));

            var warmup = SelectedWarmup ?? "none";
            var success = await _ttsClient.LoadModelAsync(
                SelectedModel, attention, quantization,
                warmup: warmup,
                warmupLang: WarmupLang,
                warmupVoice: !string.IsNullOrEmpty(WarmupVoice) ? WarmupVoice : null,
                warmupTimeout: WarmupTimeout,
                enableOptimizations: EnableOptimizations,
                torchCompile: TorchCompile,
                cudaGraphs: CudaGraphs,
                compileCodebook: CompileCodebook,
                fastCodebook: FastCodebook,
                forceCpu: ForceCpu);

            if (success)
            {
                ModelStatusText = GetLocalizedString("ModelLoaded", "Loaded");
                ModelState = "loaded";
                _rawModelServerStatus = "ready";
                UpdateIndicatorState();
                OnPropertyChanged(nameof(IsModelLoaded));

                // Sync settings
                _settings.Model.Core.Name = SelectedModel;
                _settings.Model.Core.Attention = attention;
                _settings.Model.Core.Quantization = quantization;

                // Update queue manager and orchestrator
                _queueManager.ModelName = SelectedModel;
                _queueManager.Quantization = quantization;
                _orchestrator.Model = SelectedModel;

                // Set auto-unload if enabled
                if (AutoUnload && AutoUnloadMinutes > 0)
                {
                    await _ttsClient.SetAutoUnloadAsync(SelectedModel, AutoUnloadMinutes);
                }

                SaveSettings();

                // Refresh model status
                await LoadModelsStatusAsync();
            }
            else
            {
                ModelStatusText = GetLocalizedString("ModelLoadError", "Load failed");
                ModelState = "error";
                _rawModelServerStatus = "error";
                UpdateIndicatorState();
                OnPropertyChanged(nameof(IsModelLoaded));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load model {Model}", SelectedModel);
            ModelStatusText = GetLocalizedString("ModelLoadError", "Error");
            ModelState = "error";
            _rawModelServerStatus = "error";
            UpdateIndicatorState();
            OnPropertyChanged(nameof(IsModelLoaded));
        }
        finally
        {
            IsModelOperationInProgress = false;
        }
    }

    /// <summary>
    /// Unloads the currently selected model from the TTS server,
    /// first skipping any active synthesis.
    /// </summary>
    private async Task UnloadCurrentModelAsync()
    {
        if (string.IsNullOrEmpty(SelectedModel))
            return;

        try
        {
            IsModelOperationInProgress = true;

            // Skip active synthesis before unloading
            await _orchestrator.SkipCurrentSynthesisAsync();

            var success = await _ttsClient.UnloadModelAsync(SelectedModel);

            if (success)
            {
                ModelStatusText = GetLocalizedString("ModelUnloaded", "Unloaded");
                ModelState = "unloaded";
                _rawModelServerStatus = "unloaded";
                UpdateIndicatorState();
                OnPropertyChanged(nameof(IsModelLoaded));

                // Refresh model status
                await LoadModelsStatusAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unload model {Model}", SelectedModel);
        }
        finally
        {
            IsModelOperationInProgress = false;
        }
    }

    /// <summary>
    /// Switches from the currently loaded model to a different one
    /// by unloading the old model and loading the new one.
    /// </summary>
    private async Task SwitchModelAsync(string newModel)
    {
        try
        {
            IsModelOperationInProgress = true;

            // Unload current model first
            await _orchestrator.SkipCurrentSynthesisAsync();

            // Find the loaded model and unload it
            if (_lastModelsStatus?.Models != null)
            {
                foreach (var (modelId, info) in _lastModelsStatus.Models)
                {
                    if (info.IsLoaded && modelId != newModel)
                    {
                        await _ttsClient.UnloadModelAsync(modelId);
                        break;
                    }
                }
            }

            // Load the new model
            await LoadCurrentModelAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch to model {Model}", newModel);
        }
        finally
        {
            IsModelOperationInProgress = false;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMANDS - Queue / Playback
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Skips the currently playing message.
    /// </summary>
    [RelayCommand]
    private async Task SkipMessageAsync()
    {
        await SkipCurrentAsync();
    }

    /// <summary>
    /// Internal helper to skip the current message: stops playback,
    /// cancels active synthesis, and removes the message from the queue.
    /// </summary>
    private async Task SkipCurrentAsync()
    {
        try
        {
            // Stop current playback
            _playbackController.Skip();

            // Also try to skip inference on the server
            await _orchestrator.SkipCurrentSynthesisAsync();

            // Skip the current message in the queue
            _queueManager.SkipCurrent();

            Dispatcher.UIThread.Post(() =>
            {
                StatusBarText = GetLocalizedString("SkippedMessage", "Skipped current message");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error skipping message");
        }
    }

    /// <summary>
    /// Clears the entire message queue and stops all playback.
    /// </summary>
    [RelayCommand]
    private async Task ClearQueueAsync()
    {
        try
        {
            // Stop playback
            await _playbackController.StopAsync();

            // Try to skip server inference
            await _orchestrator.SkipCurrentSynthesisAsync();

            // Clear queue
            _queueManager.Clear();

            Dispatcher.UIThread.Post(() =>
            {
                QueueItems.Clear();
                QueueGroupText = string.Format(GetLocalizedString("QueueCountFormat", "Queue ({0})"), 0);
                StatusBarText = GetLocalizedString("QueueCleared", "Queue cleared");
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing queue");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMANDS - Cache
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Removes unused entries from the audio cache to free disk space.
    /// </summary>
    [RelayCommand]
    private void CompressCache()
    {
        try
        {
            var (removedCount, freedBytes) = _cacheManager.RemoveUnusedEntries();
            var freedMb = freedBytes / (1024.0 * 1024.0);
            StatusBarText = string.Format(GetLocalizedString("CacheCompressedFormat", "Cache compressed: removed {0} entries, freed {1:F1} MB"), removedCount, freedMb);
            UpdateCacheDisplay();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compress cache");
            StatusBarText = string.Format(GetLocalizedString("ErrorWithMessageFormat", "Error: {0}"), ex.Message);
        }
    }

    /// <summary>
    /// Clears the entire audio cache from disk.
    /// Stops playback first to prevent data loss and file access conflicts.
    /// </summary>
    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        try
        {
            // Stop playback to prevent file access conflicts
            await _playbackController.StopAsync();

            _cacheManager.Clear();
            StatusBarText = GetLocalizedString("CacheCleared", "Cache cleared");
            UpdateCacheDisplay();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache");
            StatusBarText = string.Format(GetLocalizedString("ErrorWithMessageFormat", "Error: {0}"), ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMANDS - Timeout
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Applies the configured inference timeout to the TTS server.
    /// </summary>
    [RelayCommand]
    private async Task ApplyTimeoutAsync()
    {
        if (!IsServerAvailable)
        {
            StatusBarText = GetLocalizedString("TtsServerUnavailable", "TTS server unavailable");
            return;
        }

        try
        {
            var success = await _ttsClient.SetInferenceTimeoutAsync(Timeout);
            if (success)
            {
                StatusBarText = string.Format(GetLocalizedString("TimeoutSetFormat", "Generation timeout set: {0} sec"), Timeout);
            }
            else
            {
                StatusBarText = GetLocalizedString("TimeoutSetError", "Error setting timeout");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply timeout");
            StatusBarText = string.Format(GetLocalizedString("ErrorWithMessageFormat", "Error: {0}"), ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMANDS - Notifications
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Dismisses a notification by its ID (triggered by close button click).
    /// </summary>
    [RelayCommand]
    private Task DismissNotification(long notificationId) =>
        DismissNotificationInternalAsync(notificationId);

    // ═══════════════════════════════════════════════════════════
    //  COMMANDS - Language
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Changes the application UI language based on the selected language index.
    /// </summary>
    [RelayCommand]
    private void ChangeLanguage()
    {
        var langCode = SelectedLanguageIndex == 1 ? "ru" : "en";
        ChangeLanguageInternal(langCode);
    }

    /// <summary>
    /// Internal helper for changing the UI language.
    /// Can be called directly from code-behind or tests.
    /// </summary>
    /// <param name="langCode">Language code ("en" or "ru").</param>
    public void ChangeLanguageInternal(string langCode)
    {
        App.SetLanguage(langCode);
        _settings.Metadata.LanguageUI = langCode;
        SaveSettings();
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMANDS - Updates
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Manually checks for application updates (client + server).
    /// Manual check ignores skip settings — always shows if update exists.
    /// </summary>
    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        try
        {
            var updateInfo = await _updateService.CheckForUpdateAsync();
            _settings.Metadata.LastUpdateCheck = DateTime.UtcNow;
            SaveSettings();

            var hasUpdate = false;

            if (updateInfo != null)
            {
                hasUpdate = true;
                Dispatcher.UIThread.Post(() => ShowUpdateRequested?.Invoke(updateInfo));
            }

            // Also check server update
            var serverUpdateInfo = await CheckServerUpdateInternalAsync();
            if (serverUpdateInfo != null)
            {
                hasUpdate = true;
                Dispatcher.UIThread.Post(() => ShowServerUpdateRequested?.Invoke(serverUpdateInfo));
            }

            if (!hasUpdate)
            {
                ShowNotification(
                    GetLocalizedString("NoUpdatesAvailable", "You're running the latest version"),
                    NotificationSeverity.Info, durationMs: 3000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual update check failed");
            ShowNotification(
                GetLocalizedString("UpdateCheckFailed", "Failed to check for updates"),
                NotificationSeverity.Warning, durationMs: 3000);
        }
    }

    /// <summary>
    /// Checks for updates on startup with a delay. Skips if dev build or checked recently.
    /// Applies skip-version filtering for both client and server.
    /// </summary>
    private async Task CheckForUpdateOnStartupAsync()
    {
        try
        {
            if (UpdateService.IsDevBuild())
            {
                _logger.LogDebug("Skipping startup update check (dev build)");
                return;
            }

            // Skip if checked within the minimum interval
            if (_settings.Metadata.LastUpdateCheck.HasValue)
            {
                var elapsed = DateTime.UtcNow - _settings.Metadata.LastUpdateCheck.Value;
                if (elapsed.TotalMinutes < AppConstants.Update.MinCheckIntervalMinutes)
                {
                    _logger.LogDebug("Skipping startup update check (last check {Elapsed:F0}m ago)",
                        elapsed.TotalMinutes);
                    return;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(AppConstants.Update.StartupCheckDelaySeconds));

            // Client update check
            var updateInfo = await _updateService.CheckForUpdateAsync();
            _settings.Metadata.LastUpdateCheck = DateTime.UtcNow;
            SaveSettings();

            if (updateInfo != null)
            {
                // Apply skip filter for startup check
                if (updateInfo.NewVersion == _settings.Metadata.SkippedClientVersion)
                {
                    _logger.LogDebug("Skipping client update {Version} (user skipped)", updateInfo.NewVersion);
                    updateInfo = null;
                }
            }

            if (updateInfo != null)
            {
                _logger.LogInformation("Update available: {Current} → {New}",
                    updateInfo.CurrentVersion, updateInfo.NewVersion);
                Dispatcher.UIThread.Post(() => ShowUpdateRequested?.Invoke(updateInfo));
            }

            // Server update check
            var serverUpdateInfo = await CheckServerUpdateInternalAsync();
            if (serverUpdateInfo != null)
            {
                // Apply skip filter for startup check
                if (serverUpdateInfo.NewVersion == _settings.Metadata.SkippedServerVersion)
                {
                    _logger.LogDebug("Skipping server update {Version} (user skipped)", serverUpdateInfo.NewVersion);
                    serverUpdateInfo = null;
                }
            }

            if (serverUpdateInfo != null)
            {
                _logger.LogInformation("Server update available: {Current} → {New}",
                    serverUpdateInfo.CurrentVersion, serverUpdateInfo.NewVersion);
                Dispatcher.UIThread.Post(() => ShowServerUpdateRequested?.Invoke(serverUpdateInfo));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Startup update check failed");
        }
    }

    /// <summary>
    /// Fetches the server version and checks GitHub for a newer release.
    /// </summary>
    private async Task<ServerUpdateInfo?> CheckServerUpdateInternalAsync()
    {
        try
        {
            var serverVersion = await _updateService.GetServerVersionAsync(TtsServerUrl);
            if (string.IsNullOrEmpty(serverVersion))
            {
                _logger.LogDebug("Could not fetch server version from {Url}", TtsServerUrl);
                return null;
            }

            return await _updateService.CheckForServerUpdateAsync(serverVersion, TtsServerUrl);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Server update check failed");
            return null;
        }
    }

    [RelayCommand]
    private void TestClientUpdateDialog()
    {
        if (!IsDebugBuild) return;

        var mockInfo = new UpdateInfo
        {
            CurrentVersion = UpdateService.GetCurrentVersion(),
            NewVersion = "99.0.0",
            Changelog = "This is a test changelog for the client update dialog.\n\n- Feature 1\n- Feature 2\n- Bug fix",
            ReleaseUrl = "https://github.com/VirtualZer0/StreamTalkerClient/releases",
            AssetName = "StreamTalkerClient-v99.0.0-win-x64.zip",
            AssetSize = 50_000_000,
            DownloadUrl = "https://example.com/test"
        };
        Dispatcher.UIThread.Post(() => ShowUpdateRequested?.Invoke(mockInfo));
    }

    [RelayCommand]
    private void TestServerUpdateDialog()
    {
        if (!IsDebugBuild) return;

        var mockInfo = new ServerUpdateInfo
        {
            CurrentVersion = "1.0.0",
            NewVersion = "99.0.0",
            Changelog = "This is a test changelog for the server update dialog.\n\n- Server feature 1\n- Server feature 2\n- Performance improvement",
            ReleaseUrl = "https://github.com/VirtualZer0/StreamTalkerServer/releases",
            IsLocalServer = true,
            DockerComposeDir = AppDomain.CurrentDomain.BaseDirectory
        };
        Dispatcher.UIThread.Post(() => ShowServerUpdateRequested?.Invoke(mockInfo));
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMANDS - TTS Server Settings
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Applies the TTS server URL change. Updates the client connection
    /// and saves the setting.
    /// </summary>
    [RelayCommand]
    private void ApplyTtsServerUrl()
    {
        if (string.IsNullOrWhiteSpace(TtsServerUrlEdit))
        {
            StatusBarText = GetLocalizedString("InvalidServerUrl", "Invalid server URL");
            return;
        }

        // Update the actual URL property, which will trigger OnTtsServerUrlChanged
        TtsServerUrl = TtsServerUrlEdit;
        StatusBarText = GetLocalizedString("ServerUrlUpdated", "Server URL updated");
    }
}
