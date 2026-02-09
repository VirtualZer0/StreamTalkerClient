using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;

namespace StreamTalkerClient.Managers;

public class PlaybackController : IDisposable
{
    private readonly ILogger<PlaybackController> _logger;
    private readonly AudioPlayer _audioPlayer;
    private readonly MessageQueueManager _queueManager;
    private readonly CacheManager _cacheManager;
    private readonly AppSettings _settings;

    private readonly object _stateLock = new();
    private QueuedMessage? _currentMessage;
    private DateTime _lastPlaybackTime = DateTime.MinValue;
    private string? _currentCacheKey; // Track cache key for pinning
    private bool _completionHandled; // Prevents double-processing of playback completion

    public int PlaybackDelaySeconds { get; set; } = 5;

    public event EventHandler<QueuedMessage>? PlaybackStarted;
    public event EventHandler<QueuedMessage>? PlaybackFinished;
    public event EventHandler<string>? Error;

    public bool IsPlaying => _audioPlayer.IsPlaying;

    public PlaybackController(
        AudioPlayer audioPlayer,
        MessageQueueManager queueManager,
        CacheManager cacheManager,
        AppSettings settings)
    {
        _logger = AppLoggerFactory.CreateLogger<PlaybackController>();
        _audioPlayer = audioPlayer;
        _queueManager = queueManager;
        _cacheManager = cacheManager;
        _settings = settings;

        _audioPlayer.PlaybackFinished += OnAudioPlaybackFinished;
    }

    private void OnAudioPlaybackFinished(object? sender, EventArgs e)
    {
        QueuedMessage? message;
        string? cacheKey;

        lock (_stateLock)
        {
            // Prevent double-processing if completion fires multiple times
            if (_completionHandled)
                return;
            _completionHandled = true;

            message = _currentMessage;
            cacheKey = _currentCacheKey;
            _currentMessage = null;
            _currentCacheKey = null;
            _lastPlaybackTime = DateTime.UtcNow;
        }

        if (message != null)
        {
            _logger.LogDebug("Playback finished for message #{SeqNum}", message.SequenceNumber);
            _queueManager.UpdateState(message, MessageState.Done);

            // Unpin cache entry
            if (cacheKey != null)
                _cacheManager.Unpin(cacheKey);

            PlaybackFinished?.Invoke(this, message);
        }
    }

    public async Task<bool> TryPlayNextAsync()
    {
        if (_audioPlayer.IsPlaying)
            return false;

        // Cleanup orphaned message if player stopped but event didn't fire
        QueuedMessage? orphan = null;
        string? orphanCacheKey = null;
        bool delayNotElapsed = false;

        lock (_stateLock)
        {
            if (_currentMessage != null)
            {
                orphan = _currentMessage;
                orphanCacheKey = _currentCacheKey;
                _currentMessage = null;
                _currentCacheKey = null;
                _lastPlaybackTime = DateTime.UtcNow;
            }

            // Check delay
            var elapsed = (DateTime.UtcNow - _lastPlaybackTime).TotalSeconds;
            if (elapsed < PlaybackDelaySeconds && _lastPlaybackTime != DateTime.MinValue)
                delayNotElapsed = true;
        }

        // Always cleanup orphan BEFORE returning (even if delay not elapsed)
        if (orphan != null)
        {
            _logger.LogWarning("Cleaning up orphaned message #{SeqNum} stuck in Playing state",
                orphan.SequenceNumber);
            _queueManager.UpdateState(orphan, MessageState.Done);

            // Unpin orphan's cache entry
            if (orphanCacheKey != null)
                _cacheManager.Unpin(orphanCacheKey);
        }

        if (delayNotElapsed)
            return false;

        var message = _queueManager.GetNextReadyMessage();
        if (message == null)
            return false;

        return await PlayMessageAsync(message);
    }

    public bool TryPlayNext()
    {
        return TryPlayNextAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task<bool> PlayMessageAsync(QueuedMessage message)
    {
        if (message.State != MessageState.Ready)
            return false;

        string? audioPath = message.AudioFilePath;
        string cacheKey = message.CacheKey;

        // Try cache if no direct path
        if (string.IsNullOrEmpty(audioPath))
        {
            if (!_cacheManager.TryGet(cacheKey, out audioPath) || audioPath == null)
            {
                _logger.LogWarning("Audio file not found for message #{SeqNum}: {Text}",
                    message.SequenceNumber, message.GetDisplayText());
                Error?.Invoke(this, $"Audio file not found for message: {message.GetDisplayText()}");
                _queueManager.UpdateState(message, MessageState.Done);
                return false;
            }
        }

        // Pin the cache entry before playback to prevent eviction
        _cacheManager.Pin(cacheKey);

        // Set current message BEFORE marking as Playing to avoid race
        lock (_stateLock)
        {
            _currentMessage = message;
            _currentCacheKey = cacheKey;
            _completionHandled = false; // Reset for new playback
        }

        _queueManager.UpdateState(message, MessageState.Playing);

        bool playbackStarted = false;
        try
        {
            // Apply per-voice volume multiplied by main volume
            var perVoiceVolume = _settings.Audio.GetVoiceVolume(message.VoiceName);
            var mainVolume = _settings.Audio.VolumePercent;
            var finalVolume = (perVoiceVolume / 100f) * (mainVolume / 100f);
            _audioPlayer.Volume = finalVolume;

            playbackStarted = await _audioPlayer.PlayAsync(audioPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception playing audio for message #{SeqNum}", message.SequenceNumber);
        }

        if (playbackStarted)
        {
            _logger.LogDebug("Playing message #{SeqNum}: {Text}", message.SequenceNumber, message.GetDisplayText());
            PlaybackStarted?.Invoke(this, message);
            return true;
        }

        // Playback failed - cleanup state
        _logger.LogError("Failed to play audio for message #{SeqNum}", message.SequenceNumber);

        lock (_stateLock)
        {
            _currentMessage = null;
            _currentCacheKey = null;
        }

        _cacheManager.Unpin(cacheKey);
        _queueManager.UpdateState(message, MessageState.Done);
        Error?.Invoke(this, $"Failed to play audio for: {message.GetDisplayText()}");
        return false;
    }

    public bool PlayMessage(QueuedMessage message)
    {
        return PlayMessageAsync(message).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task StopAsync()
    {
        await _audioPlayer.StopAsync().ConfigureAwait(false);

        QueuedMessage? message;
        string? cacheKey;

        lock (_stateLock)
        {
            message = _currentMessage;
            cacheKey = _currentCacheKey;
            _currentMessage = null;
            _currentCacheKey = null;
        }

        if (message != null)
        {
            _queueManager.UpdateState(message, MessageState.Done);

            if (cacheKey != null)
                _cacheManager.Unpin(cacheKey);
        }
    }

    public void Stop()
    {
        StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public void Skip()
    {
        Stop();
        lock (_stateLock)
        {
            _lastPlaybackTime = DateTime.MinValue; // Allow immediate next playback
        }
    }

    public void Dispose()
    {
        _audioPlayer.PlaybackFinished -= OnAudioPlaybackFinished;
    }
}
