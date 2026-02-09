using System.Collections.ObjectModel;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;

namespace StreamTalkerClient.ViewModels;

/// <summary>
/// Settings load/save, reward caching, voice loading, and initial data load
/// for MainWindowViewModel.
/// </summary>
public partial class MainWindowViewModel
{
    // ═══════════════════════════════════════════════════════════
    //  SETTINGS LOAD / SAVE
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Loads all saved settings from the AppSettings object into the ViewModel
    /// observable properties, and configures services with the saved values.
    /// Must be called before event wiring to avoid feedback loops.
    /// </summary>
    private void LoadSettingsToProperties()
    {
        // Twitch
        Channel = _settings.Services.Twitch.Channel;
        ReadAllMessages = _settings.Services.Twitch.ReadAllMessages;
        RequireVoice = _settings.Services.Twitch.RequireVoice;

        // VK Play
        VkChannel = _settings.Services.VKPlay.Channel;
        VkReadAllMessages = _settings.Services.VKPlay.ReadAllMessages;
        VkRequireVoice = _settings.Services.VKPlay.RequireVoice;

        // Server / TTS
        TtsServerUrl = _settings.Server.BaseUrl;
        TtsServerUrlEdit = _settings.Server.BaseUrl;
        PlaybackDelay = _settings.Audio.PlaybackDelaySeconds;
        CacheLimitMB = _settings.Cache.LimitMB;

        // Model
        SelectedModel = _settings.Model.Core.Name;
        SelectedAttention = _settings.Model.Core.Attention;
        SelectedQuantization = _settings.Model.Core.Quantization;
        DoSample = _settings.Inference.DoSample;
        AutoUnload = _settings.Model.AutoUnload.Enabled;
        AutoUnloadMinutes = _settings.Model.AutoUnload.Minutes;

        // Model settings
        SelectedWarmup = _settings.Model.Warmup.Mode;
        ForceCpu = _settings.Model.Core.ForceCpu;
        SelectedTtsLanguage = _settings.Server.Language;

        // Optimization flags
        EnableOptimizations = _settings.Model.Optimizations.Enabled;
        TorchCompile = _settings.Model.Optimizations.TorchCompile;
        CudaGraphs = _settings.Model.Optimizations.CudaGraphs;
        CompileCodebook = _settings.Model.Optimizations.CompileCodebook;
        FastCodebook = _settings.Model.Optimizations.FastCodebook;

        // Warmup sub-parameters
        WarmupLang = _settings.Model.Warmup.Language;
        WarmupVoice = _settings.Model.Warmup.Voice;
        WarmupTimeout = _settings.Model.Warmup.TimeoutSeconds;

        // Note: Warmup voice selection will be restored after voices are loaded from server
        // (see LoadVoicesFromServerAsync)

        // Voice synthesis parameters
        Speed = _settings.Voice.Speed;
        Temperature = _settings.Voice.Temperature;
        MaxNewTokens = _settings.Voice.MaxNewTokens;
        RepetitionPenalty = _settings.Voice.RepetitionPenalty;

        // Voice extraction mode
        SelectedExtractionModeIndex = _settings.Voice.VoiceExtractionMode == "firstword" ? 1 : 0;

        // Volume & Hotkeys
        Volume = _settings.Audio.VolumePercent;
        VolumeText = $"{_settings.Audio.VolumePercent}%";

        // Configure hotkey service
        _hotkeyService.SkipCurrentKey = GlobalHotkeyService.ParseKeyCode(_settings.Hotkeys.SkipCurrentKey);
        _hotkeyService.SkipAllKey = GlobalHotkeyService.ParseKeyCode(_settings.Hotkeys.SkipAllKey);
        _hotkeyService.Enabled = _settings.Hotkeys.Enabled;
        UpdateHotkeysText();

        // Queue manager settings
        _queueManager.DefaultVoice = _settings.Voice.DefaultVoice;
        _queueManager.ModelName = _settings.Model.Core.Name;
        _queueManager.Quantization = _settings.Model.Core.Quantization;
        _queueManager.DoSample = _settings.Inference.DoSample;
        _queueManager.RequireVoice = _settings.Services.Twitch.RequireVoice;
        _queueManager.VoiceExtractionMode = _settings.Voice.VoiceExtractionMode;
        _queueManager.Language = _settings.Server.Language;
        _queueManager.Speed = _settings.Voice.Speed;
        _queueManager.Temperature = _settings.Voice.Temperature;
        _queueManager.MaxNewTokens = _settings.Voice.MaxNewTokens;
        _queueManager.RepetitionPenalty = _settings.Voice.RepetitionPenalty;

        // Orchestrator settings
        _orchestrator.Model = _settings.Model.Core.Name;
        _orchestrator.Language = _settings.Server.Language;
        _orchestrator.DoSample = _settings.Inference.DoSample;
        _orchestrator.MaxBatchSize = _settings.Inference.MaxBatchSize;
        _orchestrator.Speed = _settings.Voice.Speed;
        _orchestrator.Temperature = _settings.Voice.Temperature;
        _orchestrator.MaxNewTokens = _settings.Voice.MaxNewTokens;
        _orchestrator.RepetitionPenalty = _settings.Voice.RepetitionPenalty;

        // Playback controller
        _playbackController.PlaybackDelaySeconds = _settings.Audio.PlaybackDelaySeconds;

        // Load cached rewards from settings
        LoadCachedRewards();

        // Language
        SelectedLanguageIndex = _settings.Metadata.LanguageUI == "ru" ? 1 : 0;

        // UI state
        ActiveServiceTabIndex = _settings.Ui.ActiveServiceTab;
        ServicesExpanded = _settings.Ui.ServicesExpanded;
        VoiceSettingsExpanded = _settings.Ui.VoiceSettingsExpanded;
        ModelControlExpanded = _settings.Ui.ModelControlExpanded;
        QueueExpanded = _settings.Ui.QueueExpanded;
        CacheExpanded = _settings.Ui.CacheExpanded;
        SettingsExpanded = _settings.Ui.SettingsExpanded;
        IsQueuePanelOpen = _settings.Ui.IsQueuePanelOpen;
    }

    /// <summary>
    /// Saves all current ViewModel property values back to the AppSettings
    /// object and persists them to disk.
    /// </summary>
    private void SaveSettings()
    {
        if (!_isInitialized)
            return;

        try
        {
            // Twitch
            _settings.Services.Twitch.Channel = Channel;
            _settings.Services.Twitch.ReadAllMessages = ReadAllMessages;
            _settings.Services.Twitch.RequireVoice = RequireVoice;

            // Twitch reward
            if (SelectedRewardIndex > 0 && SelectedRewardIndex <= _twitchService.CustomRewards.Length)
            {
                _settings.Services.Twitch.RewardId = _twitchService.CustomRewards[SelectedRewardIndex - 1].id;
            }
            else
            {
                _settings.Services.Twitch.RewardId = null;
            }

            // VK Play
            _settings.Services.VKPlay.Channel = VkChannel;
            _settings.Services.VKPlay.ReadAllMessages = VkReadAllMessages;
            _settings.Services.VKPlay.RequireVoice = VkRequireVoice;

            // VK Play reward
            if (SelectedVkRewardIndex > 0 && SelectedVkRewardIndex <= _vkPlayService.CustomRewards.Length)
            {
                _settings.Services.VKPlay.RewardId = _vkPlayService.CustomRewards[SelectedVkRewardIndex - 1].Id;
            }
            else
            {
                _settings.Services.VKPlay.RewardId = null;
            }

            // Server
            _settings.Server.BaseUrl = TtsServerUrl;
            _settings.Audio.PlaybackDelaySeconds = PlaybackDelay;
            _settings.Cache.LimitMB = CacheLimitMB;

            // Model
            _settings.Model.Core.Name = SelectedModel ?? "0.6B";
            _settings.Model.Core.Attention = SelectedAttention ?? "auto";
            _settings.Model.Core.Quantization = SelectedQuantization ?? "none";
            _settings.Inference.DoSample = DoSample;
            _settings.Model.AutoUnload.Enabled = AutoUnload;
            _settings.Model.AutoUnload.Minutes = AutoUnloadMinutes;
            _settings.Model.Warmup.Mode = SelectedWarmup ?? "none";
            _settings.Model.Core.ForceCpu = ForceCpu;
            _settings.Model.Optimizations.Enabled = EnableOptimizations;
            _settings.Model.Optimizations.TorchCompile = TorchCompile;
            _settings.Model.Optimizations.CudaGraphs = CudaGraphs;
            _settings.Model.Optimizations.CompileCodebook = CompileCodebook;
            _settings.Model.Optimizations.FastCodebook = FastCodebook;
            _settings.Model.Warmup.Language = WarmupLang;
            _settings.Model.Warmup.Voice = WarmupVoice;
            _settings.Model.Warmup.TimeoutSeconds = WarmupTimeout;
            _settings.Voice.Speed = Speed;
            _settings.Voice.Temperature = Temperature;
            _settings.Voice.MaxNewTokens = MaxNewTokens;
            _settings.Voice.RepetitionPenalty = RepetitionPenalty;

            // Voice
            if (SelectedVoice != null)
            {
                _settings.Voice.DefaultVoice = SelectedVoice.Name;
            }

            // Voice extraction mode
            _settings.Voice.VoiceExtractionMode = SelectedExtractionModeIndex == 1 ? "firstword" : "bracket";

            // Volume
            _settings.Audio.VolumePercent = Volume;

            // Language
            _settings.Metadata.LanguageUI = SelectedLanguageIndex == 1 ? "ru" : "en";

            // UI state
            _settings.Ui.ActiveServiceTab = ActiveServiceTabIndex;
            _settings.Ui.ServicesExpanded = ServicesExpanded;
            _settings.Ui.VoiceSettingsExpanded = VoiceSettingsExpanded;
            _settings.Ui.ModelControlExpanded = ModelControlExpanded;
            _settings.Ui.QueueExpanded = QueueExpanded;
            _settings.Ui.CacheExpanded = CacheExpanded;
            _settings.Ui.SettingsExpanded = SettingsExpanded;
            _settings.Ui.IsQueuePanelOpen = IsQueuePanelOpen;

            _settings.Save();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  IMPORT SETTINGS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Imports settings from an external AppSettings object, copies values
    /// to the current settings, saves to disk, and reloads all ViewModel properties.
    /// </summary>
    public void ImportSettings(AppSettings imported)
    {
        // Copy all user-facing settings from imported to current
        _settings.Services.Twitch.Channel = imported.Services.Twitch.Channel;
        _settings.Services.Twitch.RewardId = imported.Services.Twitch.RewardId;
        _settings.Services.Twitch.ReadAllMessages = imported.Services.Twitch.ReadAllMessages;
        _settings.Services.Twitch.RequireVoice = imported.Services.Twitch.RequireVoice;
        _settings.Voice.DefaultVoice = imported.Voice.DefaultVoice;
        _settings.Server.BaseUrl = imported.Server.BaseUrl;
        _settings.Model.Core.Name = imported.Model.Core.Name;
        _settings.Model.Core.Attention = imported.Model.Core.Attention;
        _settings.Model.Core.Quantization = imported.Model.Core.Quantization;
        _settings.Inference.DoSample = imported.Inference.DoSample;
        _settings.Server.Language = imported.Server.Language;
        _settings.Metadata.LanguageUI = imported.Metadata.LanguageUI;
        _settings.Audio.PlaybackDelaySeconds = imported.Audio.PlaybackDelaySeconds;
        _settings.Cache.LimitMB = imported.Cache.LimitMB;
        _settings.Model.AutoUnload.Enabled = imported.Model.AutoUnload.Enabled;
        _settings.Model.AutoUnload.Minutes = imported.Model.AutoUnload.Minutes;
        _settings.Model.Warmup.Mode = imported.Model.Warmup.Mode;
        _settings.Model.Core.ForceCpu = imported.Model.Core.ForceCpu;
        _settings.Model.Optimizations.Enabled = imported.Model.Optimizations.Enabled;
        _settings.Model.Optimizations.TorchCompile = imported.Model.Optimizations.TorchCompile;
        _settings.Model.Optimizations.CudaGraphs = imported.Model.Optimizations.CudaGraphs;
        _settings.Model.Optimizations.CompileCodebook = imported.Model.Optimizations.CompileCodebook;
        _settings.Model.Optimizations.FastCodebook = imported.Model.Optimizations.FastCodebook;
        _settings.Model.Warmup.Language = imported.Model.Warmup.Language;
        _settings.Model.Warmup.Voice = imported.Model.Warmup.Voice;
        _settings.Model.Warmup.TimeoutSeconds = imported.Model.Warmup.TimeoutSeconds;
        _settings.Voice.Speed = imported.Voice.Speed;
        _settings.Voice.Temperature = imported.Voice.Temperature;
        _settings.Voice.MaxNewTokens = imported.Voice.MaxNewTokens;
        _settings.Voice.RepetitionPenalty = imported.Voice.RepetitionPenalty;
        _settings.Audio.VolumePercent = imported.Audio.VolumePercent;
        _settings.Audio.VoiceVolumes = imported.Audio.VoiceVolumes;
        _settings.Voice.VoiceExtractionMode = imported.Voice.VoiceExtractionMode;
        _settings.Voice.VoiceBindings = imported.Voice.VoiceBindings;
        _settings.Services.VKPlay.Channel = imported.Services.VKPlay.Channel;
        _settings.Services.VKPlay.RewardId = imported.Services.VKPlay.RewardId;
        _settings.Services.VKPlay.ReadAllMessages = imported.Services.VKPlay.ReadAllMessages;
        _settings.Services.VKPlay.RequireVoice = imported.Services.VKPlay.RequireVoice;

        // Save and reload
        _settings.Save();
        LoadSettingsToProperties();

        // Apply language
        App.SetLanguage(_settings.Metadata.LanguageUI);
    }

    // ═══════════════════════════════════════════════════════════
    //  REWARD CACHING
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Loads cached rewards for both platforms from saved settings.
    /// </summary>
    private void LoadCachedRewards()
    {
        LoadCachedRewardsForPlatform(
            _settings.Services.TwitchRewardsCache,
            r => _twitchService.CustomRewards = r,
            Rewards, r => r.title,
            _settings.Services.Twitch.RewardId, r => r.id,
            i => SelectedRewardIndex = i);

        LoadCachedRewardsForPlatform(
            _settings.Services.VKRewardsCache,
            r => _vkPlayService.CustomRewards = r,
            VkRewards, r => r.Title,
            _settings.Services.VKPlay.RewardId, r => r.Id,
            i => SelectedVkRewardIndex = i);
    }

    /// <summary>
    /// Generic helper to load cached rewards for a single platform.
    /// Populates the rewards collection and restores the previously selected reward.
    /// </summary>
    /// <typeparam name="T">Platform-specific reward type.</typeparam>
    /// <param name="cachedRewards">Rewards array from settings cache.</param>
    /// <param name="setServiceRewards">Setter to assign rewards to the streaming service.</param>
    /// <param name="rewardsCollection">Observable collection for the UI dropdown.</param>
    /// <param name="titleSelector">Function to extract display title from a reward.</param>
    /// <param name="savedRewardId">Previously saved reward ID to restore selection.</param>
    /// <param name="idSelector">Function to extract ID from a reward.</param>
    /// <param name="setIndex">Setter for the selected index property.</param>
    private static void LoadCachedRewardsForPlatform<T>(
        T[] cachedRewards,
        Action<T[]> setServiceRewards,
        ObservableCollection<string> rewardsCollection,
        Func<T, string?> titleSelector,
        string? savedRewardId,
        Func<T, string?> idSelector,
        Action<int> setIndex)
    {
        if (cachedRewards.Length == 0) return;

        setServiceRewards(cachedRewards);
        rewardsCollection.Clear();
        rewardsCollection.Add(GetLocalizedString("ReadAllMessagesOption", "(Read all messages)"));
        foreach (var reward in cachedRewards)
        {
            rewardsCollection.Add(titleSelector(reward) ?? GetLocalizedString("UnnamedReward", "(unnamed)"));
        }
        SelectRewardById(savedRewardId, cachedRewards, idSelector, setIndex);
    }

    // ═══════════════════════════════════════════════════════════
    //  REWARD SELECTION HELPERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Selects the Twitch reward matching the given ID.
    /// Delegates to the generic <see cref="SelectRewardById{T}"/> method.
    /// </summary>
    private void SelectTwitchRewardById(string? rewardId) =>
        SelectRewardById(rewardId, _twitchService.CustomRewards, r => r.id, i => SelectedRewardIndex = i);

    /// <summary>
    /// Selects the VK Play reward matching the given ID.
    /// Delegates to the generic <see cref="SelectRewardById{T}"/> method.
    /// </summary>
    private void SelectVkRewardById(string? rewardId) =>
        SelectRewardById(rewardId, _vkPlayService.CustomRewards, r => r.Id, i => SelectedVkRewardIndex = i);

    /// <summary>
    /// Generic reward selection by ID. Searches the reward array for a matching ID
    /// and sets the selected index via the provided setter (index 0 = "Read all messages").
    /// </summary>
    /// <typeparam name="T">The reward type (platform-specific).</typeparam>
    /// <param name="rewardId">The reward ID to find, or null/empty for "all messages".</param>
    /// <param name="rewards">The array of platform rewards to search.</param>
    /// <param name="idSelector">Function to extract the ID from a reward object.</param>
    /// <param name="setIndex">Setter for the selected reward index property.</param>
    private static void SelectRewardById<T>(string? rewardId, T[] rewards, Func<T, string?> idSelector, Action<int> setIndex)
    {
        if (string.IsNullOrEmpty(rewardId))
        {
            setIndex(0);
            return;
        }

        for (int i = 0; i < rewards.Length; i++)
        {
            if (idSelector(rewards[i]) == rewardId)
            {
                setIndex(i + 1); // +1 because index 0 is "(Read all messages)"
                return;
            }
        }

        setIndex(0);
    }

    // ═══════════════════════════════════════════════════════════
    //  INITIAL DATA LOAD
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Performs the initial data load on startup: checks server health,
    /// loads voices, model status, GPU info, and inference timeout.
    /// </summary>
    private async Task InitialDataLoadAsync()
    {
        try
        {
            // Check server health
            var isHealthy = await _ttsClient.CheckHealthAsync();
            UpdateServerStatus(isHealthy);

            if (isHealthy)
            {
                // Load voices
                await LoadVoicesFromServerAsync();

                // Load models status
                await LoadModelsStatusAsync();

                // Load GPU info
                await LoadGpuInfoAsync();

                // Load inference timeout
                await LoadInferenceTimeoutAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial data load");
        }
    }

    /// <summary>
    /// Fetches available voices from the TTS server and populates the AvailableVoices
    /// collection. Restores the previously saved default voice selection.
    /// </summary>
    private async Task LoadVoicesFromServerAsync()
    {
        try
        {
            var voices = await _ttsClient.GetVoicesAsync();
            Dispatcher.UIThread.Post(() =>
            {
                AvailableVoices.Clear();
                foreach (var voice in voices)
                {
                    AvailableVoices.Add(voice);
                }

                // Update queue manager with available voice names
                _queueManager.UpdateAvailableVoices(voices.Select(v => v.Name));

                // Select saved default voice
                if (!string.IsNullOrEmpty(_settings.Voice.DefaultVoice))
                {
                    var match = AvailableVoices.FirstOrDefault(v =>
                        string.Equals(v.Name, _settings.Voice.DefaultVoice, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        SelectedVoice = match;
                    }
                }

                // If no voice selected and voices available, select first
                if (SelectedVoice == null && AvailableVoices.Count > 0)
                {
                    SelectedVoice = AvailableVoices[0];
                }

                // Restore warmup voice selection (VoiceInfo match by name)
                if (!string.IsNullOrEmpty(_settings.Model.Warmup.Voice))
                {
                    var warmupMatch = AvailableVoices.FirstOrDefault(v =>
                        string.Equals(v.Name, _settings.Model.Warmup.Voice, StringComparison.OrdinalIgnoreCase));
                    if (warmupMatch != null)
                    {
                        SelectedWarmupVoice = warmupMatch;
                    }
                }

                // If no warmup voice selected and voices available, select first
                if (SelectedWarmupVoice == null && AvailableVoices.Count > 0)
                {
                    SelectedWarmupVoice = AvailableVoices[0];
                }

                _logger.LogInformation("Loaded {Count} voices from server", voices.Count);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load voices from server");
        }
    }

    /// <summary>
    /// Fetches the current models status from the TTS server and updates
    /// the UI model list and status display.
    /// </summary>
    private async Task LoadModelsStatusAsync()
    {
        try
        {
            var status = await _ttsClient.GetModelsStatusAsync();
            if (status?.Models != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateModelsFromStatus(status);
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load models status");
        }
    }

    /// <summary>
    /// Fetches GPU information (max VRAM and current usage) from the TTS server.
    /// </summary>
    private async Task LoadGpuInfoAsync()
    {
        try
        {
            // Get max VRAM info
            var maxVram = await _ttsClient.GetMaxVramAsync();
            if (maxVram != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    MaxVramSliderMax = (int)maxVram.TotalVramMb;
                    MaxVramSliderValue = (int)maxVram.MaxVramMb;
                    MaxVramText = string.Format(GetLocalizedString("MaxVramDetailFormat", "Max VRAM: {0:F0} / {1:F0} MB"), maxVram.MaxVramMb, maxVram.TotalVramMb);
                    UpdateVramLimit();
                });
            }

            // Get current GPU usage
            var gpuUsage = await _ttsClient.GetGpuUsageAsync();
            if (gpuUsage != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateGpuDisplay(gpuUsage);
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load GPU info");
        }
    }

    /// <summary>
    /// Fetches the current inference timeout setting from the TTS server.
    /// </summary>
    private async Task LoadInferenceTimeoutAsync()
    {
        try
        {
            var timeout = await _ttsClient.GetInferenceTimeoutAsync();
            if (timeout.HasValue)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Timeout = timeout.Value;
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load inference timeout");
        }
    }
}
