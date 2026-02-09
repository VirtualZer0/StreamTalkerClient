using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.ViewModels;

/// <summary>
/// Event handler setup and streaming platform event handling for MainWindowViewModel.
/// Wires up events from streaming services, TTS connection manager, queue manager,
/// synthesis orchestrator, and global hotkey service.
/// </summary>
public partial class MainWindowViewModel
{
    // ═══════════════════════════════════════════════════════════
    //  EVENT HANDLER SETUP
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Wires up connection state change, message, and reward events for both
    /// Twitch and VK Play streaming services.
    /// </summary>
    private void SetupStreamingEventHandlers()
    {
        // Twitch connection state changes
        _twitchService.OnConnectionStateChanged += (_, state) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                TwitchConnectionState = state;
                UpdateTwitchStatusDisplay(state);
            });
        };

        // Twitch unified message handler
        _twitchService.OnMessage += (_, message) =>
        {
            _messageFilterService.HandleStreamMessage(message, "Twitch", ReadAllMessages, RequireVoice,
                SelectedRewardIndex, null);
        };

        // Twitch unified reward handler
        _twitchService.OnReward += (_, message) =>
        {
            _messageFilterService.HandleStreamReward(message, "Twitch",
                SelectedRewardIndex, _twitchService.Rewards);
        };

        // VK Play connection state changes
        _vkPlayService.OnConnectionStateChanged += (_, state) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                VkConnectionState = state;
                UpdateVkStatusDisplay(state);
            });
        };

        // VK Play unified message handler
        _vkPlayService.OnMessage += (_, message) =>
        {
            _messageFilterService.HandleStreamMessage(message, "VKPlay", VkReadAllMessages, VkRequireVoice,
                SelectedVkRewardIndex, null);
        };

        // VK Play unified reward handler
        _vkPlayService.OnReward += (_, message) =>
        {
            _messageFilterService.HandleStreamReward(message, "VKPlay",
                SelectedVkRewardIndex, _vkPlayService.Rewards);
        };
    }

    /// <summary>
    /// Wires up TTS connection manager events for server availability changes,
    /// voice reloads, model status reloads, and GPU usage updates.
    /// </summary>
    private void SetupTtsConnectionEvents()
    {
        _ttsConnectionManager.ServerAvailabilityChanged += (_, available) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateServerStatus(available);
                _orchestrator.ServerAvailable = available;
            });
        };

        _ttsConnectionManager.VoicesReloaded += (_, voices) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                AvailableVoices.Clear();
                foreach (var voice in voices)
                {
                    AvailableVoices.Add(voice);
                }
                _queueManager.UpdateAvailableVoices(voices.Select(v => v.Name));

                // Re-select previous voice
                if (!string.IsNullOrEmpty(_settings.Voice.DefaultVoice))
                {
                    var match = AvailableVoices.FirstOrDefault(v =>
                        string.Equals(v.Name, _settings.Voice.DefaultVoice, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        SelectedVoice = match;
                    }
                }

                _logger.LogInformation("Voices reloaded: {Count} voices", voices.Count);
            });
        };

        _ttsConnectionManager.ModelsReloaded += (_, status) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateModelsFromStatus(status);
            });
        };

        _ttsConnectionManager.GpuUsageUpdated += (_, gpuUsage) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                UpdateGpuDisplay(gpuUsage);
            });
        };
    }

    /// <summary>
    /// Wires up queue manager events for message lifecycle notifications
    /// (added, state changed, invalid voice, failed) and queue activity detection.
    /// </summary>
    private void SetupQueueEvents()
    {
        _queueManager.MessageAdded += (_, msg) =>
        {
            _logger.LogDebug("Message queued #{SeqNum}: [{Voice}] {Text}",
                msg.SequenceNumber, msg.VoiceName, msg.GetDisplayText());
        };

        _queueManager.MessageStateChanged += (_, msg) =>
        {
            _logger.LogDebug("Message #{SeqNum} state -> {State}",
                msg.SequenceNumber, msg.State);
        };

        _queueManager.InvalidVoice += (_, args) =>
        {
            _logger.LogWarning("Invalid voice '{Voice}' from user {Username}",
                args.VoiceName, args.Username);
            Dispatcher.UIThread.Post(() =>
            {
                StatusBarText = string.Format(GetLocalizedString("InvalidVoiceFormat", "Invalid voice '{0}' from {1}"), args.VoiceName, args.Username);
            });
        };

        _queueManager.MessageFailed += (_, msg) =>
        {
            _logger.LogWarning("Message #{SeqNum} failed: {Text}",
                msg.SequenceNumber, msg.GetDisplayText());
        };

        // Wake up synthesis loop immediately on new message activity (idle mode optimization)
        _queueManager.MessageQueueActivityDetected += (_, _) => _orchestrator.WakeUp();
    }

    /// <summary>
    /// Wires up synthesis orchestrator events for status changes, errors,
    /// batch synthesis start/complete, and playback lifecycle.
    /// </summary>
    private void SetupOrchestratorEvents()
    {
        _orchestrator.StatusChanged += (_, status) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusBarText = status;
            });
        };

        _orchestrator.Error += (_, error) =>
        {
            _logger.LogError("Orchestrator error: {Error}", error);
            Dispatcher.UIThread.Post(() =>
            {
                StatusBarText = error;
            });
        };

        _orchestrator.BatchSynthesisStarted += (_, args) =>
        {
            _logger.LogDebug("Batch synthesis started: {Count} messages, voice: {Voice}",
                args.BatchSize, args.Voice);
            Dispatcher.UIThread.Post(() =>
            {
                _synthDebounceCts?.Cancel();
                _synthDebounceCts?.Dispose();
                _synthDebounceCts = null;
                IsSynthesizing = true;
                UpdateIndicatorState();
            });
        };

        _orchestrator.BatchSynthesisCompleted += (_, count) =>
        {
            _logger.LogDebug("Batch synthesis completed: {Count} messages", count);
            Dispatcher.UIThread.Post(() =>
            {
                _synthDebounceCts?.Cancel();
                _synthDebounceCts?.Dispose();
                var cts = _synthDebounceCts = new CancellationTokenSource();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(500, cts.Token);
                        Dispatcher.UIThread.Post(() =>
                        {
                            IsSynthesizing = false;
                            UpdateIndicatorState();
                        });
                    }
                    catch (OperationCanceledException) { }
                });
            });
        };

        _playbackController.PlaybackStarted += (_, msg) =>
        {
            _logger.LogInformation("Playing: [{Voice}] {Text}",
                msg.VoiceName, msg.GetDisplayText());
        };

        _playbackController.PlaybackFinished += (_, msg) =>
        {
            _logger.LogDebug("Playback finished: #{SeqNum}", msg.SequenceNumber);
        };

        _playbackController.Error += (_, error) =>
        {
            _logger.LogError("Playback error: {Error}", error);
        };
    }

    /// <summary>
    /// Wires up global hotkey events for skip-current and skip-all (clear queue) actions.
    /// </summary>
    private void SetupHotkeyEvents()
    {
        _hotkeyService.SkipCurrentPressed += (_, _) =>
        {
            _logger.LogDebug("Hotkey: Skip current");
            _ = SkipCurrentAsync();
        };

        _hotkeyService.SkipAllPressed += (_, _) =>
        {
            _logger.LogDebug("Hotkey: Skip all / Clear queue");
            _ = ClearQueueAsync();
        };
    }
}
