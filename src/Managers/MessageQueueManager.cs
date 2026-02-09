using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Managers;

public class MessageQueueManager : IDisposable
{
    private readonly ILogger<MessageQueueManager> _logger;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<QueuedMessage>> _voiceQueues = new();
    private readonly ConcurrentDictionary<long, QueuedMessage> _allMessages = new();
    private readonly object _lock = new();

    private static readonly Regex VoicePattern = new(@"^\[([^\]]+)\]\s*(.*)$", RegexOptions.Singleline);
    private static readonly Regex FirstWordPattern = new(@"^(\S+)\s+(.+)$", RegexOptions.Singleline);

    private HashSet<string> _availableVoices = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public string DefaultVoice { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string Quantization { get; set; } = "none";
    public bool DoSample { get; set; } = true;
    public bool RequireVoice { get; set; } = false;
    public string VoiceExtractionMode { get; set; } = "bracket";
    
    // Default synthesis parameters (used when creating messages)
    public string Language { get; set; } = "Russian";
    public double Speed { get; set; } = 1.0;
    public double Temperature { get; set; } = 0.7;
    public int MaxNewTokens { get; set; } = 2000;
    public double RepetitionPenalty { get; set; } = 1.05;

    public event EventHandler<QueuedMessage>? MessageAdded;
    public event EventHandler<QueuedMessage>? MessageStateChanged;
    public event EventHandler<(string Username, string VoiceName, string Text)>? InvalidVoice;
    public event EventHandler<QueuedMessage>? MessageFailed;
    public event EventHandler? MessageQueueActivityDetected;

    public int TotalCount => _allMessages.Count;
    public int PendingCount => _allMessages.Values.Count(m => m.State == MessageState.Queued);
    public int SynthesizingCount => _allMessages.Values.Count(m => m.State == MessageState.Synthesizing);
    public int ReadyCount => _allMessages.Values.Count(m => m.State == MessageState.Ready);

    public MessageQueueManager()
    {
        _logger = AppLoggerFactory.CreateLogger<MessageQueueManager>();
    }

    /// <summary>
    /// Updates the list of available voices from the server.
    /// </summary>
    public void UpdateAvailableVoices(IEnumerable<string> voices)
    {
        lock (_lock)
        {
            _availableVoices = new HashSet<string>(voices, StringComparer.OrdinalIgnoreCase);
        }
        _logger.LogDebug("Updated available voices: {Count} voices", _availableVoices.Count);
    }

    /// <summary>
    /// Checks if a voice is available on the server.
    /// </summary>
    public bool IsVoiceAvailable(string voiceName)
    {
        if (string.IsNullOrEmpty(voiceName))
            return false;

        lock (_lock)
        {
            // If no voices are loaded yet, allow all (will fail at synthesis time if invalid)
            if (_availableVoices.Count == 0)
                return true;

            return _availableVoices.Contains(voiceName);
        }
    }

    public QueuedMessage? AddMessage(string text, string username)
    {
        return AddMessage(text, username, RequireVoice);
    }

    /// <summary>
    /// Adds a message to the queue with platform-specific requireVoice override.
    /// </summary>
    /// <param name="text">The message text.</param>
    /// <param name="username">The username who sent the message.</param>
    /// <param name="requireVoice">Whether a voice prefix is required for this specific message.</param>
    public QueuedMessage? AddMessage(string text, string username, bool requireVoice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var (voiceName, cleanedText, hasExplicitVoice) = ExtractVoice(text);

        // Skip message if voice is required but not explicitly specified
        if (requireVoice && !hasExplicitVoice)
        {
            _logger.LogDebug("Message from {User} skipped: no voice prefix", username);
            return null;
        }

        return AddMessageInternal(text, cleanedText, voiceName, username, hasExplicitVoice, isBound: false);
    }

    /// <summary>
    /// Adds a message with a bound voice, bypassing RequireVoice check.
    /// Explicit [voiceName] syntax in the message overrides the bound voice.
    /// </summary>
    public QueuedMessage? AddMessageWithBinding(string text, string username, string boundVoiceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundVoiceName);

        // Check for explicit voice override [voiceName] syntax
        var (explicitVoice, cleanedText, hasExplicitVoice) = ExtractVoice(text);

        // Use explicit voice if provided, otherwise use bound voice
        var voiceName = hasExplicitVoice ? explicitVoice : boundVoiceName;
        var finalText = hasExplicitVoice ? cleanedText : text.Trim();

        return AddMessageInternal(text, finalText, voiceName, username, hasExplicitVoice, isBound: true);
    }

    /// <summary>
    /// Adds a message directly to the queue with explicit voice name and custom synthesis parameters.
    /// Used for manually added messages with full control over synthesis settings.
    /// </summary>
    public QueuedMessage? AddManualMessage(
        string text,
        string voiceName,
        string language,
        double speed,
        double temperature,
        int maxNewTokens,
        double repetitionPenalty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(voiceName);

        return AddMessageInternal(
            text,
            text,
            voiceName,
            "Manual",
            hasExplicitVoice: true,
            isBound: false,
            language: language,
            speed: speed,
            temperature: temperature,
            maxNewTokens: maxNewTokens,
            repetitionPenalty: repetitionPenalty);
    }

    /// <summary>
    /// Common logic for adding messages to the queue.
    /// </summary>
    private QueuedMessage? AddMessageInternal(
        string originalText,
        string cleanedText,
        string voiceName,
        string username,
        bool hasExplicitVoice,
        bool isBound,
        string? language = null,
        double? speed = null,
        double? temperature = null,
        int? maxNewTokens = null,
        double? repetitionPenalty = null)
    {
        // Validate voice exists before queuing
        if (!IsVoiceAvailable(voiceName))
        {
            var logPrefix = isBound ? "Bound message" : "Message";
            _logger.LogWarning("{Type} from {User} skipped: voice '{Voice}' not found", logPrefix, username, voiceName);
            InvalidVoice?.Invoke(this, (username, voiceName, cleanedText));
            return null;
        }

        var message = new QueuedMessage(
            originalText,
            cleanedText,
            voiceName,
            username,
            ModelName,
            Quantization,
            DoSample,
            language ?? Language,
            speed ?? Speed,
            temperature ?? Temperature,
            maxNewTokens ?? MaxNewTokens,
            repetitionPenalty ?? RepetitionPenalty);

        lock (_lock)
        {
            _allMessages[message.SequenceNumber] = message;

            var queue = _voiceQueues.GetOrAdd(voiceName.ToLowerInvariant(), _ => new ConcurrentQueue<QueuedMessage>());
            queue.Enqueue(message);
        }

        if (isBound)
        {
            _logger.LogDebug("Bound message #{SeqNum} added: [{Voice}] from {User} (override={Override})",
                message.SequenceNumber, voiceName, username, hasExplicitVoice);
        }
        else
        {
            _logger.LogDebug("Message #{SeqNum} added: [{Voice}] from {User}",
                message.SequenceNumber, voiceName, username);
        }

        MessageAdded?.Invoke(this, message);
        MessageQueueActivityDetected?.Invoke(this, EventArgs.Empty);
        return message;
    }

    /// <summary>
    /// Calculate TTS length - each digit counts as 5 symbols (TTS expands numbers)
    /// </summary>
    private static int GetTtsLength(string text)
    {
        var length = 0;

        foreach (var c in text)
        {
            if (char.IsDigit(c))
                length += 5;
            else
                length += 1;
        }

        return length;
    }

    private (string voice, string text, bool hasExplicitVoice) ExtractVoice(string message)
    {
        if (VoiceExtractionMode == "firstword")
        {
            var match = FirstWordPattern.Match(message);
            if (match.Success)
            {
                var potentialVoice = match.Groups[1].Value;
                var text = match.Groups[2].Value.Trim();

                // Only treat first word as voice if it exists in available voices
                if (!string.IsNullOrEmpty(text) && IsVoiceAvailable(potentialVoice))
                {
                    return (potentialVoice, text, true);
                }
            }
            return (DefaultVoice, message.Trim(), false);
        }

        // Default: bracket mode [voice] text
        var bracketMatch = VoicePattern.Match(message);
        if (bracketMatch.Success)
        {
            var voice = bracketMatch.Groups[1].Value.Trim();
            var text = bracketMatch.Groups[2].Value.Trim();
            if (!string.IsNullOrEmpty(voice) && !string.IsNullOrEmpty(text))
            {
                return (voice, text, true);
            }
        }

        return (DefaultVoice, message.Trim(), false);
    }

    /// <summary>
    /// Checks if a message has a voice tag without processing it.
    /// </summary>
    public bool HasVoiceTag(string message)
    {
        var (_, _, hasExplicitVoice) = ExtractVoice(message);
        return hasExplicitVoice;
    }

    /// <summary>
    /// Removes a message from the queue (used when synthesis fails after max retries).
    /// </summary>
    public void RemoveMessage(QueuedMessage message)
    {
        lock (_lock)
        {
            UpdateStateInternal(message, MessageState.Done);
        }
        MessageFailed?.Invoke(this, message);
    }

    public List<QueuedMessage>? GetNextBatch(int maxBatchSize, int maxTotalLength = 200)
    {
        lock (_lock)
        {
            // Find the voice queue whose first message has the lowest sequence number
            string? selectedVoice = null;
            long lowestSequence = long.MaxValue;

            foreach (var kvp in _voiceQueues)
            {
                if (kvp.Value.TryPeek(out var firstMsg) &&
                    firstMsg.State == MessageState.Queued &&
                    firstMsg.SequenceNumber < lowestSequence)
                {
                    lowestSequence = firstMsg.SequenceNumber;
                    selectedVoice = kvp.Key;
                }
            }

            if (selectedVoice == null)
                return null;

            var queue = _voiceQueues[selectedVoice];
            var batch = new List<QueuedMessage>();
            var totalLength = 0;

            // Dequeue messages until we hit count limit or total length limit
            while (batch.Count < maxBatchSize && queue.TryPeek(out var msg) && msg.State == MessageState.Queued)
            {
                var msgLength = GetTtsLength(msg.CleanedText);

                // Check if adding this message would exceed the length limit
                // Always allow at least one message in the batch
                if (batch.Count > 0 && totalLength + msgLength > maxTotalLength)
                    break;

                if (queue.TryDequeue(out var dequeuedMsg))
                {
                    batch.Add(dequeuedMsg);
                    totalLength += msgLength;
                }
            }

            return batch.Count > 0 ? batch : null;
        }
    }

    public QueuedMessage? GetNextReadyMessage()
    {
        lock (_lock)
        {
            // Get the ready message with the lowest sequence number
            var readyMessage = _allMessages.Values
                .Where(m => m.State == MessageState.Ready)
                .OrderBy(m => m.SequenceNumber)
                .FirstOrDefault();

            if (readyMessage == null)
                return null;

            // Check if there are any messages with lower sequence numbers still processing
            // (Queued, Synthesizing, or WaitingForCache)
            // If so, we must wait for them to complete first to maintain order
            var hasBlockingMessage = _allMessages.Values
                .Any(m => m.SequenceNumber < readyMessage.SequenceNumber &&
                         (m.State == MessageState.Queued ||
                          m.State == MessageState.Synthesizing ||
                          m.State == MessageState.WaitingForCache));

            return hasBlockingMessage ? null : readyMessage;
        }
    }

    public void UpdateState(QueuedMessage message, MessageState newState)
    {
        lock (_lock)
        {
            message.State = newState;

            if (newState == MessageState.Done)
            {
                // Remove from tracking
                _allMessages.TryRemove(message.SequenceNumber, out _);
            }
        }

        // Fire event outside lock to prevent deadlocks
        MessageStateChanged?.Invoke(this, message);
    }

    /// <summary>
    /// Update state without firing event - for internal use only
    /// </summary>
    private void UpdateStateInternal(QueuedMessage message, MessageState newState)
    {
        // Must be called while holding _lock
        message.State = newState;

        if (newState == MessageState.Done)
        {
            _allMessages.TryRemove(message.SequenceNumber, out _);
        }
    }

    public void UpdateBatchState(List<QueuedMessage> messages, MessageState newState)
    {
        // Update all states inside lock, collect for event firing
        lock (_lock)
        {
            foreach (var msg in messages)
            {
                UpdateStateInternal(msg, newState);
            }
        }

        // Fire events outside lock
        foreach (var msg in messages)
        {
            MessageStateChanged?.Invoke(this, msg);
        }
    }

    /// <summary>
    /// Re-enqueue messages back to their voice queues (used when synthesis fails)
    /// </summary>
    public void RequeueMessages(List<QueuedMessage> messages)
    {
        lock (_lock)
        {
            foreach (var msg in messages)
            {
                UpdateStateInternal(msg, MessageState.Queued);
                var queue = _voiceQueues.GetOrAdd(msg.VoiceName.ToLowerInvariant(), _ => new ConcurrentQueue<QueuedMessage>());
                queue.Enqueue(msg);
            }
        }

        // Fire events outside lock
        foreach (var msg in messages)
        {
            MessageStateChanged?.Invoke(this, msg);
        }
    }

    public List<QueuedMessage> GetAllMessages()
    {
        lock (_lock)
        {
            return _allMessages.Values
                .Where(m => m.State != MessageState.Done)
                .OrderBy(m => m.SequenceNumber)
                .ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _voiceQueues.Clear();
            _allMessages.Clear();
        }

        _logger.LogDebug("Message queue cleared");
    }

    public void SkipCurrent()
    {
        QueuedMessage? playing;

        lock (_lock)
        {
            playing = _allMessages.Values.FirstOrDefault(m => m.State == MessageState.Playing);
            if (playing != null)
            {
                UpdateStateInternal(playing, MessageState.Done);
            }
        }

        // Fire event outside lock to prevent deadlocks
        if (playing != null)
        {
            MessageStateChanged?.Invoke(this, playing);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Clear();

        // Clear event handlers
        MessageAdded = null;
        MessageStateChanged = null;
    }
}
