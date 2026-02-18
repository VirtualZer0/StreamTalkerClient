using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;

namespace StreamTalkerClient.Managers;

public class SynthesisOrchestrator : IDisposable
{
    private readonly ILogger<SynthesisOrchestrator> _logger;
    private readonly QwenTtsClient _ttsClient;
    private readonly MessageQueueManager _queueManager;
    private readonly CacheManager _cacheManager;
    private readonly PlaybackController _playbackController;

    private CancellationTokenSource? _cts;
    private Task? _synthesisLoop;
    private Task? _playbackLoop;

    // Track cache keys currently being synthesized to avoid duplicate synthesis
    private readonly HashSet<string> _synthesizingCacheKeys = new();
    private readonly object _cacheKeyLock = new();

    // Timeout for messages stuck in WaitingForCache state
    private static readonly TimeSpan WaitingForCacheTimeout = TimeSpan.FromMinutes(2);

    // Server availability flag - when false, synthesis loop waits instead of attempting requests
    private volatile bool _serverAvailable = true;
    private readonly object _serverAvailableLock = new();

    // Idle mode detection for CPU optimization (stored as ticks for atomic access via Interlocked)
    private long _lastActivityTimeTicks = DateTime.UtcNow.Ticks;
    private static readonly TimeSpan IdleThreshold = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim _wakeupSignal = new(0, 1);

    public string Model { get; set; } = "1.7B";
    public string Language { get; set; } = "Russian";
    public bool DoSample { get; set; } = true;
    public int MaxBatchSize { get; set; } = 2;
    public double Speed { get; set; } = Infrastructure.AppConstants.Synthesis.DefaultSpeed;
    public double Temperature { get; set; } = Infrastructure.AppConstants.Synthesis.DefaultTemperature;
    public int MaxNewTokens { get; set; } = Infrastructure.AppConstants.Synthesis.DefaultMaxNewTokens;
    public double RepetitionPenalty { get; set; } = Infrastructure.AppConstants.Synthesis.DefaultRepetitionPenalty;

    public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

    /// <summary>
    /// Gets or sets whether the TTS server is available.
    /// When false, the synthesis loop waits instead of attempting requests.
    /// </summary>
    public bool ServerAvailable
    {
        get => _serverAvailable;
        set
        {
            lock (_serverAvailableLock)
            {
                if (_serverAvailable == value)
                    return;

                _serverAvailable = value;
            }

            if (value)
            {
                _logger.LogInformation("TTS server is now available, resuming synthesis");
                StatusChanged?.Invoke(this, "TTS server available");
            }
            else
            {
                _logger.LogWarning("TTS server is unavailable, pausing synthesis");
                StatusChanged?.Invoke(this, "TTS server unavailable - waiting...");
            }
        }
    }

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<string>? Error;
    public event EventHandler<(int BatchSize, string Voice)>? BatchSynthesisStarted;
    public event EventHandler<int>? BatchSynthesisCompleted;

    public SynthesisOrchestrator(
        QwenTtsClient ttsClient,
        MessageQueueManager queueManager,
        CacheManager cacheManager,
        PlaybackController playbackController)
    {
        _logger = AppLoggerFactory.CreateLogger<SynthesisOrchestrator>();
        _ttsClient = ttsClient;
        _queueManager = queueManager;
        _cacheManager = cacheManager;
        _playbackController = playbackController;
    }

    public void Start()
    {
        if (IsRunning)
            return;

        _cts = new CancellationTokenSource();

        _synthesisLoop = Task.Run(() => SynthesisLoopAsync(_cts.Token));
        _playbackLoop = Task.Run(() => PlaybackLoopAsync(_cts.Token));

        _logger.LogInformation("Orchestrator started");
        StatusChanged?.Invoke(this, "Orchestrator started");
    }

    /// <summary>
    /// Wake up the synthesis loop immediately when new messages are added.
    /// Optimizes CPU usage by reducing loop delay from 1000ms to 100ms.
    /// </summary>
    public void WakeUp()
    {
        Interlocked.Exchange(ref _lastActivityTimeTicks, DateTime.UtcNow.Ticks);
        if (_wakeupSignal.CurrentCount == 0)
        {
            try
            {
                _wakeupSignal.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already signaled, no problem
            }
        }
    }

    /// <summary>
    /// Request stop without waiting - fire and forget
    /// </summary>
    public void RequestStop()
    {
        if (_cts == null)
            return;

        _cts.Cancel();
        _logger.LogInformation("Orchestrator stop requested");
        StatusChanged?.Invoke(this, "Orchestrator stopping...");

        // Clear tracking immediately
        lock (_cacheKeyLock)
        {
            _synthesizingCacheKeys.Clear();
        }
    }

    public void Stop()
    {
        StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task StopAsync()
    {
        if (_cts == null)
            return;

        _cts.Cancel();

        // Wait for background tasks to complete with timeout
        var tasks = new List<Task>();
        if (_synthesisLoop != null) tasks.Add(_synthesisLoop);
        if (_playbackLoop != null) tasks.Add(_playbackLoop);

        if (tasks.Count > 0)
        {
            try
            {
                // Wait with timeout to prevent hanging on shutdown
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await Task.WhenAll(tasks).WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Background tasks did not complete within timeout");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error waiting for background tasks to complete");
            }
        }

        _cts.Dispose();
        _cts = null;
        _synthesisLoop = null;
        _playbackLoop = null;

        lock (_cacheKeyLock)
        {
            _synthesizingCacheKeys.Clear();
        }

        _logger.LogInformation("Orchestrator stopped");
        StatusChanged?.Invoke(this, "Orchestrator stopped");
    }

    /// <summary>
    /// Request server to skip/abort current inference.
    /// The synthesis loop will handle message removal when the request fails.
    /// </summary>
    public async Task SkipCurrentSynthesisAsync()
    {
        _logger.LogInformation("Requesting inference skip");

        // Tell server to abort inference - this will cause the current synthesis request to fail
        // The synthesis loop will handle removing messages when it receives the error response
        await _ttsClient.SkipInferenceAsync();
    }

    private async Task SynthesisLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Wait if server is unavailable - check every second
            if (!_serverAvailable)
            {
                await Task.Delay(Infrastructure.AppConstants.Intervals.ErrorRetryDelayMs, ct);
                continue;
            }

            // Track current batch and cache keys for cleanup
            List<QueuedMessage>? currentBatch = null;
            HashSet<string>? currentCacheKeys = null;

            try
            {
                var batch = _queueManager.GetNextBatch(MaxBatchSize);

                if (batch == null || batch.Count == 0)
                {
                    // Check for timed-out WaitingForCache messages
                    ResolveOrTimeoutWaitingMessages();

                    // Use dynamic delay based on activity - reduce CPU when idle
                    var lastActivity = new DateTime(Interlocked.Read(ref _lastActivityTimeTicks), DateTimeKind.Utc);
                    var timeSinceActivity = DateTime.UtcNow - lastActivity;
                    var isIdle = timeSinceActivity > IdleThreshold;
                    var delay = isIdle ? Infrastructure.AppConstants.Intervals.ErrorRetryDelayMs : Infrastructure.AppConstants.Intervals.SynthesisLoopDelayMs;

                    try
                    {
                        // Use semaphore for immediate wake-up on new messages
                        await _wakeupSignal.WaitAsync(delay, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }

                    continue;
                }

                // Activity detected - reset idle timer
                Interlocked.Exchange(ref _lastActivityTimeTicks, DateTime.UtcNow.Ticks);

                // Check cache and in-progress synthesis for each message
                var needsSynthesis = new List<QueuedMessage>();
                var cacheKeysToSynthesize = new HashSet<string>();

                foreach (var msg in batch)
                {
                    // First check cache
                    if (_cacheManager.TryGet(msg.CacheKey, out var cachedPath))
                    {
                        msg.SetSynthesisResult(cachedPath, wasCacheHit: true);
                        _queueManager.UpdateState(msg, MessageState.Ready);
                        _logger.LogDebug("Cache hit for message #{SeqNum}", msg.SequenceNumber);
                        continue;
                    }

                    // Check if this cache key is already being synthesized
                    bool alreadySynthesizing;
                    lock (_cacheKeyLock)
                    {
                        alreadySynthesizing = _synthesizingCacheKeys.Contains(msg.CacheKey);
                    }

                    if (alreadySynthesizing)
                    {
                        // Another message with same text is being synthesized, wait for it
                        _queueManager.UpdateState(msg, MessageState.WaitingForCache);
                        _logger.LogDebug("Message #{SeqNum} waiting for cache (duplicate of in-progress synthesis)", msg.SequenceNumber);
                        continue;
                    }

                    // Check if another message in this batch has the same cache key
                    if (cacheKeysToSynthesize.Contains(msg.CacheKey))
                    {
                        // Duplicate within batch, wait for the first one
                        _queueManager.UpdateState(msg, MessageState.WaitingForCache);
                        _logger.LogDebug("Message #{SeqNum} waiting for cache (duplicate in batch)", msg.SequenceNumber);
                        continue;
                    }

                    // Need to synthesize this message
                    needsSynthesis.Add(msg);
                    cacheKeysToSynthesize.Add(msg.CacheKey);
                }

                if (needsSynthesis.Count == 0)
                {
                    // Check if any WaitingForCache messages can now be resolved
                    ResolveOrTimeoutWaitingMessages();
                    continue;
                }

                // Track for cleanup on exception
                currentBatch = needsSynthesis;
                currentCacheKeys = cacheKeysToSynthesize;

                // Mark cache keys as being synthesized
                lock (_cacheKeyLock)
                {
                    foreach (var key in cacheKeysToSynthesize)
                    {
                        _synthesizingCacheKeys.Add(key);
                    }
                }

                // Mark as synthesizing
                _queueManager.UpdateBatchState(needsSynthesis, MessageState.Synthesizing);

                var voice = needsSynthesis[0].VoiceName;
                BatchSynthesisStarted?.Invoke(this, (needsSynthesis.Count, voice));
                StatusChanged?.Invoke(this, $"Synthesizing {needsSynthesis.Count} message(s) with voice: {voice}");

                _logger.LogInformation("Synthesizing batch of {Count} messages with voice: {Voice}",
                    needsSynthesis.Count, voice);

                // Synthesize batch (use parameters from first message, including its captured ModelName)
                var firstMessage = needsSynthesis[0];
                var texts = needsSynthesis.Select(m => m.CleanedText).ToArray();
                var results = await _ttsClient.SynthesizeBatchAsync(
                    texts, voice, firstMessage.ModelName, firstMessage.Language, firstMessage.DoSample,
                    speed: firstMessage.Speed, temperature: firstMessage.Temperature,
                    maxNewTokens: firstMessage.MaxNewTokens, repetitionPenalty: firstMessage.RepetitionPenalty,
                    cancellationToken: ct);

                // Clear cache keys from tracking
                lock (_cacheKeyLock)
                {
                    foreach (var key in cacheKeysToSynthesize)
                    {
                        _synthesizingCacheKeys.Remove(key);
                    }
                }

                // Clear tracking since we're handling the batch
                currentBatch = null;
                currentCacheKeys = null;

                if (results == null)
                {
                    _logger.LogWarning("Synthesis failed/skipped for batch of {Count} messages", needsSynthesis.Count);

                    // Remove failed messages from queue (no retry)
                    foreach (var msg in needsSynthesis)
                    {
                        _logger.LogDebug("Removing message #{SeqNum} due to synthesis failure", msg.SequenceNumber);
                        _queueManager.RemoveMessage(msg);
                    }

                    // Also remove waiting messages that depended on this batch
                    RemoveWaitingMessagesForKeys(cacheKeysToSynthesize);

                    continue;
                }

                // Store results in cache and update messages
                int successCount = 0;
                for (int i = 0; i < needsSynthesis.Count; i++)
                {
                    var msg = needsSynthesis[i];

                    if (results.TryGetValue(i, out var audioData))
                    {
                        var filePath = _cacheManager.Store(msg.CacheKey, audioData);
                        msg.SetSynthesisResult(filePath, wasCacheHit: false);
                        _queueManager.UpdateState(msg, MessageState.Ready);
                        successCount++;
                    }
                    else
                    {
                        _logger.LogWarning("No audio data for message #{SeqNum}: {Text}",
                            msg.SequenceNumber, msg.GetDisplayText());
                        _queueManager.RemoveMessage(msg);
                    }
                }

                // Resolve any messages waiting for these cache keys
                ResolveOrTimeoutWaitingMessages();

                BatchSynthesisCompleted?.Invoke(this, successCount);
                _logger.LogInformation("Synthesis complete: {Success}/{Total} successful", successCount, needsSynthesis.Count);
                StatusChanged?.Invoke(this, $"Synthesis complete: {successCount}/{needsSynthesis.Count} successful");
            }
            catch (OperationCanceledException)
            {
                // Orchestrator is stopping - cleanup and exit
                CleanupBatch(currentBatch, currentCacheKeys);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Synthesis loop error");
                Error?.Invoke(this, $"Synthesis error: {ex.Message}");

                // Cleanup on exception
                CleanupBatch(currentBatch, currentCacheKeys);

                await Task.Delay(Infrastructure.AppConstants.Intervals.ErrorRetryDelayMs, ct);
            }
        }
    }

    /// <summary>
    /// Clean up a batch when an exception occurs
    /// </summary>
    private void CleanupBatch(List<QueuedMessage>? batch, HashSet<string>? cacheKeys)
    {
        if (cacheKeys != null)
        {
            lock (_cacheKeyLock)
            {
                foreach (var key in cacheKeys)
                {
                    _synthesizingCacheKeys.Remove(key);
                }
            }
        }

        if (batch != null)
        {
            foreach (var msg in batch)
            {
                _queueManager.RemoveMessage(msg);
            }

            if (cacheKeys != null)
            {
                RemoveWaitingMessagesForKeys(cacheKeys);
            }
        }
    }

    /// <summary>
    /// Check for messages in WaitingForCache state:
    /// - Mark them Ready if cache is now available
    /// - Remove them if they've been waiting too long (timeout)
    /// </summary>
    private void ResolveOrTimeoutWaitingMessages()
    {
        var waitingMessages = _queueManager.GetAllMessages()
            .Where(m => m.State == MessageState.WaitingForCache)
            .ToList();

        var now = DateTime.UtcNow;

        foreach (var msg in waitingMessages)
        {
            if (_cacheManager.TryGet(msg.CacheKey, out var cachedPath))
            {
                msg.SetSynthesisResult(cachedPath, wasCacheHit: true);
                _queueManager.UpdateState(msg, MessageState.Ready);
                _logger.LogDebug("Message #{SeqNum} resolved from cache (was waiting)", msg.SequenceNumber);
            }
            else
            {
                // Check for timeout
                var waitStart = msg.WaitingForCacheStartTime;
                if (waitStart.HasValue && (now - waitStart.Value) > WaitingForCacheTimeout)
                {
                    _logger.LogWarning("Message #{SeqNum} timed out waiting for cache", msg.SequenceNumber);
                    _queueManager.RemoveMessage(msg);
                }
            }
        }
    }

    /// <summary>
    /// Remove waiting messages whose cache keys failed synthesis
    /// </summary>
    private void RemoveWaitingMessagesForKeys(HashSet<string> failedCacheKeys)
    {
        var waitingMessages = _queueManager.GetAllMessages()
            .Where(m => m.State == MessageState.WaitingForCache && failedCacheKeys.Contains(m.CacheKey))
            .ToList();

        if (waitingMessages.Count > 0)
        {
            _logger.LogDebug("Removing {Count} waiting messages after synthesis failure", waitingMessages.Count);
            foreach (var msg in waitingMessages)
            {
                _queueManager.RemoveMessage(msg);
            }
        }
    }

    private async Task PlaybackLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _playbackController.TryPlayNextAsync();

                // Use dynamic delay based on activity - reduce CPU when idle
                var lastActivity = new DateTime(Interlocked.Read(ref _lastActivityTimeTicks), DateTimeKind.Utc);
                var timeSinceActivity = DateTime.UtcNow - lastActivity;
                var isIdle = timeSinceActivity > IdleThreshold;
                var delay = isIdle ? Infrastructure.AppConstants.Intervals.ErrorRetryDelayMs : Infrastructure.AppConstants.Intervals.PlaybackLoopDelayMs;

                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Playback loop error");
                Error?.Invoke(this, $"Playback error: {ex.Message}");
                await Task.Delay(Infrastructure.AppConstants.Intervals.ErrorRetryDelayMs, ct);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _wakeupSignal.Dispose();
    }
}
