namespace StreamTalkerClient.Infrastructure;

/// <summary>
/// Application-wide constants to avoid magic numbers and strings.
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// Timeout values in milliseconds or as TimeSpan.
    /// </summary>
    public static class Timeouts
    {
        public static readonly TimeSpan HttpRequest = TimeSpan.FromSeconds(5);
        public static readonly TimeSpan TtsSynthesis = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan OrchestratorShutdown = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Polling and loop interval values.
    /// </summary>
    public static class Intervals
    {
        public const int UiUpdateMs = 250;
        public const int PlaybackCheckMs = 100;
        public const int SynthesisLoopDelayMs = 100;
        public const int PlaybackLoopDelayMs = 100;
        public const int ErrorRetryDelayMs = 1000;
        public const int CacheSaveDebounceMs = 2000;
        public const int ModelStatusCheckSeconds = 5;
    }

    /// <summary>
    /// Size and count limits.
    /// </summary>
    public static class Limits
    {
        public const int DefaultCacheLimitMB = 150;
        public const int MinCacheLimitMB = 10;
        public const int MaxCacheLimitMB = 10000;
        public const int DefaultMaxBatchSize = 2;
        public const int MinBatchSize = 1;
        public const int MaxBatchSize = 6;
        public const int DefaultPlaybackDelaySeconds = 5;
        public const int MaxPlaybackDelaySeconds = 300;
        public const int DefaultAutoUnloadMinutes = 15;
        public const int MinAutoUnloadMinutes = 1;
        public const int MaxAutoUnloadMinutes = 1440;
        public const int MaxQueueDisplayItems = 50;
        public const int MaxDisplayTextLength = 30;
    }

    /// <summary>
    /// Default URL and endpoint values.
    /// </summary>
    public static class Urls
    {
        public const string DefaultTtsBaseUrl = "http://localhost:7860";
        public const string TwitchGqlEndpoint = "https://gql.twitch.tv/gql";

        public static class TtsEndpoints
        {
            public const string Health = "/health";
            public const string Voices = "/voices";
            public const string SynthesizeSpeech = "/synthesize_speech/";
            public const string ModelsStatus = "/models/status";
            public const string ModelLoad = "/models/{0}/load";
            public const string ModelUnload = "/models/{0}/unload";
            public const string ModelAutoUnload = "/models/{0}/auto-unload";
        }
    }

    /// <summary>
    /// Default model and voice settings.
    /// </summary>
    public static class Defaults
    {
        public const string Model = "1.7B";
        public const string Language = "Auto";
        public const string CacheFileExtension = ".wav";
    }

    /// <summary>
    /// Canonical option lists — single source of truth for all valid values.
    /// Used by UI dropdowns, settings validation, and manual message dialog.
    /// </summary>
    public static class Options
    {
        public static readonly IReadOnlyList<string> TtsLanguages = new[]
        {
            "Auto", "Chinese", "English", "Japanese", "Korean", "French", "German",
            "Spanish", "Portuguese", "Russian", "Italian"
        };

        public static readonly IReadOnlyList<string> AttentionModes = new[]
        {
            "auto", "sage_attn", "flex_attn", "flash2_attn", "sdpa", "eager"
        };

        public static readonly IReadOnlyList<string> QuantizationModes = new[]
        {
            "none", "int8", "float8"
        };

        public static readonly IReadOnlyList<string> WarmupModes = new[]
        {
            "none", "single", "batch"
        };

        public static readonly IReadOnlyList<string> ExtractionModes = new[]
        {
            "bracket", "firstword"
        };

        /// <summary>Platform values as stored in settings (storage format).</summary>
        public static readonly IReadOnlyList<string> Platforms = new[]
        {
            "Any", "Twitch", "VKPlay"
        };

        /// <summary>Platform display names for UI dropdowns.</summary>
        public static readonly IReadOnlyList<string> PlatformDisplayNames = new[]
        {
            "Any", "Twitch", "VK Play"
        };
    }

    /// <summary>
    /// File and folder paths.
    /// </summary>
    public static class Paths
    {
        public const string DataFolder = "data";
        public const string CacheFolder = "cache";
        public const string LogsFolder = "logs";
        public const string SettingsFileName = "settings.json";
        public const string CacheIndexFileName = "index.json";
    }

    /// <summary>
    /// Cache management thresholds and settings.
    /// </summary>
    public static class Cache
    {
        public const double EvictionThresholdPercent = 0.85;
        public const int IndexSaveDebounceMs = 2000;
    }

    /// <summary>
    /// Animation and visual feedback durations.
    /// </summary>
    public static class Animation
    {
        public const int DefaultDurationMs = 300;
        public const int NotificationDismissMs = 500;
        public const double DimmedOpacity = 0.6;
        public const double SubtleOpacity = 0.3;
    }

    /// <summary>
    /// TTS synthesis default parameters.
    /// </summary>
    public static class Synthesis
    {
        public const double DefaultSpeed = 1.0;
        public const double DefaultTemperature = 0.7;
        public const int DefaultMaxNewTokens = 2000;
        public const double DefaultRepetitionPenalty = 1.05;
    }

    /// <summary>
    /// Twitch-specific constants.
    /// </summary>
    public static class Twitch
    {
        public const string AnonymousUserPrefix = "justinfan";
        public const int AnonymousUserMinId = 200;
        public const int AnonymousUserMaxId = 9999;
    }

    /// <summary>
    /// First-launch wizard constants.
    /// </summary>
    public static class Wizard
    {
        public const int HealthCheckIntervalMs = 1000;
        public const int AutoAdvanceDelayMs = 2000;
        public const int RetryDelayMs = 500;
    }

    /// <summary>
    /// Update system constants.
    /// </summary>
    public static class Update
    {
        public const string GitHubRepoOwner = "VirtualZer0";
        public const string GitHubRepoName = "StreamTalkerClient";
        public const string ServerGitHubRepoName = "StreamTalkerServer";
        public const string GitHubApiBase = "https://api.github.com";
        public const string AssetPrefix = "StreamTalkerClient-v";
        public const string UpdateFolderName = "updates";
        public const int CheckTimeoutSeconds = 15;
        public const int DownloadTimeoutMinutes = 10;
        public const int StartupCheckDelaySeconds = 5;
        public const int MinCheckIntervalMinutes = 60;
        public const string ExecutableName = "StreamTalker";
    }
}
