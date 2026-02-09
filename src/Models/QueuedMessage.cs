using System.Security.Cryptography;
using System.Text;

namespace StreamTalkerClient.Models;

public class QueuedMessage
{
    private static long _globalSequence = 0;

    private readonly object _lock = new();
    private MessageState _state;
    private string? _audioFilePath;
    private bool _wasCacheHit;
    private DateTime? _waitingForCacheStartTime;

    public long SequenceNumber { get; }
    public DateTime ArrivalTime { get; }
    public string OriginalText { get; }
    public string CleanedText { get; }
    public string VoiceName { get; }
    public string CacheKey { get; }
    public string Username { get; }
    public string ModelName { get; }
    public string Quantization { get; }
    public bool DoSample { get; }
    
    // Synthesis parameters (can be customized per message)
    public string Language { get; }
    public double Speed { get; }
    public double Temperature { get; }
    public int MaxNewTokens { get; }
    public double RepetitionPenalty { get; }

    public MessageState State
    {
        get { lock (_lock) return _state; }
        set
        {
            lock (_lock)
            {
                _state = value;
                // Track when message enters WaitingForCache state
                if (value == MessageState.WaitingForCache)
                {
                    _waitingForCacheStartTime ??= DateTime.UtcNow;
                }
                else
                {
                    _waitingForCacheStartTime = null;
                }
            }
        }
    }

    public string? AudioFilePath
    {
        get { lock (_lock) return _audioFilePath; }
        set { lock (_lock) _audioFilePath = value; }
    }

    public bool WasCacheHit
    {
        get { lock (_lock) return _wasCacheHit; }
        set { lock (_lock) _wasCacheHit = value; }
    }

    /// <summary>
    /// Time when message entered WaitingForCache state, for timeout tracking
    /// </summary>
    public DateTime? WaitingForCacheStartTime
    {
        get { lock (_lock) return _waitingForCacheStartTime; }
    }

    public QueuedMessage(
        string originalText,
        string cleanedText,
        string voiceName,
        string username,
        string modelName,
        string quantization = "none",
        bool doSample = true,
        string language = "Russian",
        double speed = 1.0,
        double temperature = 0.7,
        int maxNewTokens = 2000,
        double repetitionPenalty = 1.05)
    {
        SequenceNumber = Interlocked.Increment(ref _globalSequence);
        ArrivalTime = DateTime.UtcNow;
        OriginalText = originalText;
        CleanedText = cleanedText;
        VoiceName = voiceName;
        Username = username;
        ModelName = modelName;
        Quantization = quantization;
        DoSample = doSample;
        Language = language;
        Speed = speed;
        Temperature = temperature;
        MaxNewTokens = maxNewTokens;
        RepetitionPenalty = repetitionPenalty;
        CacheKey = GenerateCacheKey(modelName, voiceName, cleanedText, quantization, doSample, language, speed, temperature, maxNewTokens, repetitionPenalty);
        _state = MessageState.Queued;
    }

    /// <summary>
    /// Atomically sets synthesis result (path and cache hit flag)
    /// </summary>
    public void SetSynthesisResult(string? path, bool wasCacheHit)
    {
        lock (_lock)
        {
            _audioFilePath = path;
            _wasCacheHit = wasCacheHit;
        }
    }

    /// <summary>
    /// Generates a unique cache key from all synthesis parameters that affect audio output.
    /// Includes model, voice, text, quantization, sampling mode, and all inference parameters.
    /// </summary>
    private static string GenerateCacheKey(
        string model,
        string voice,
        string text,
        string quantization,
        bool doSample,
        string language,
        double speed,
        double temperature,
        int maxNewTokens,
        double repetitionPenalty)
    {
        var input = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{model}:{voice}:{quantization}:{doSample}:{language}:{speed}:{temperature}:{maxNewTokens}:{repetitionPenalty}:{text}");
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }

    public string GetDisplayText(int maxLength = 30)
    {
        var text = CleanedText.Length > maxLength
            ? CleanedText[..(maxLength - 3)] + "..."
            : CleanedText;
        return text.Replace('\n', ' ').Replace('\r', ' ');
    }

    public string GetStateIcon()
    {
        var state = State; // Read once under lock
        var cacheHit = WasCacheHit; // Read once under lock
        return state switch
        {
            MessageState.Queued => "\u23F3",
            MessageState.Synthesizing => "\uD83E\uDDE0",
            MessageState.WaitingForCache => "\u267B\uFE0F",
            MessageState.Ready => cacheHit ? "\uD83D\uDCBE" : "\u2728",
            MessageState.Playing => "\uD83D\uDD0A",
            MessageState.Done => "\u2705",
            _ => "\u2753"
        };
    }
}
