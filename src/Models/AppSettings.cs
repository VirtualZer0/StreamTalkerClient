using System.Globalization;
using StreamTalkerClient.Classes.APIModels.TwitchGQL;
using StreamTalkerClient.Infrastructure;

namespace StreamTalkerClient.Models;

public class AppSettings
{
    // ═══════════════════════════════════════════════════════════
    //  NESTED SETTINGS SECTIONS
    // ═══════════════════════════════════════════════════════════

    public PlatformServicesSettings Services { get; set; } = new();
    public VoiceSettings Voice { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public TtsServerSettings Server { get; set; } = new();
    public ModelSettings Model { get; set; } = new();
    public InferenceSettings Inference { get; set; } = new();
    public CacheSettings Cache { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
    public MetadataSettings Metadata { get; set; } = new();

    // ═══════════════════════════════════════════════════════════
    //  STATIC MEMBERS
    // ═══════════════════════════════════════════════════════════

    /// <summary>Path to the runtime data folder.</summary>
    public static readonly string DataFolder = "data";

    /// <summary>Path to the audio cache folder.</summary>
    public static readonly string CacheFolder = Path.Combine(DataFolder, "cache");

    static AppSettings()
    {
        // Ensure data and cache folders exist at startup
        if (!Directory.Exists(DataFolder))
            Directory.CreateDirectory(DataFolder);
        if (!Directory.Exists(CacheFolder))
            Directory.CreateDirectory(CacheFolder);
    }

    /// <summary>
    /// Convenience method that delegates to <see cref="SettingsRepository.Save"/>.
    /// Allows existing callers to continue using <c>_settings.Save()</c>.
    /// </summary>
    public void Save() => SettingsRepository.Save(this);

    // ═══════════════════════════════════════════════════════════
    //  NESTED CLASS DEFINITIONS
    // ═══════════════════════════════════════════════════════════

    public class PlatformServicesSettings
    {
        public PlatformSettings Twitch { get; set; } = new();
        public PlatformSettings VKPlay { get; set; } = new();
        public CustomReward[] TwitchRewardsCache { get; set; } = [];
        public VKReward[] VKRewardsCache { get; set; } = [];
    }

    public class PlatformSettings
    {
        public string Channel { get; set; } = "";
        public string? RewardId { get; set; }
        public bool ReadAllMessages { get; set; } = false;
        public bool RequireVoice { get; set; } = false;
    }

    public class VoiceSettings
    {
        public string DefaultVoice { get; set; } = "";
        public string VoiceExtractionMode { get; set; } = "bracket";
        public List<VoiceBinding> VoiceBindings { get; set; } = new();
        public double Speed { get; set; } = AppConstants.Synthesis.DefaultSpeed;
        public double Temperature { get; set; } = AppConstants.Synthesis.DefaultTemperature;
        public int MaxNewTokens { get; set; } = AppConstants.Synthesis.DefaultMaxNewTokens;
        public double RepetitionPenalty { get; set; } = AppConstants.Synthesis.DefaultRepetitionPenalty;

        /// <summary>
        /// Gets the active voice binding for a specific username (case-insensitive).
        /// Returns null if no binding exists or if the binding is disabled.
        /// </summary>
        public VoiceBinding? GetActiveBinding(string username)
        {
            return VoiceBindings.FirstOrDefault(b =>
                b.IsEnabled &&
                string.Equals(b.Username, username, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class AudioSettings
    {
        public int VolumePercent { get; set; } = 100;
        public Dictionary<string, int> VoiceVolumes { get; set; } = new();
        public int PlaybackDelaySeconds { get; set; } = 5;

        /// <summary>
        /// Gets the volume for a specific voice, falling back to global volume if not set.
        /// </summary>
        public int GetVoiceVolume(string voiceName)
        {
            return VoiceVolumes.TryGetValue(voiceName, out var volume) ? volume : VolumePercent;
        }

        /// <summary>
        /// Sets the volume for a specific voice.
        /// </summary>
        public void SetVoiceVolume(string voiceName, int volume)
        {
            VoiceVolumes[voiceName] = Math.Clamp(volume, 0, 100);
        }
    }

    public class TtsServerSettings
    {
        public string BaseUrl { get; set; } = "http://localhost:7860";
        public string Language { get; set; } = GetDefaultLanguage();

        private static string GetDefaultLanguage()
        {
            try
            {
                var culture = CultureInfo.CurrentUICulture;
                var langCode = culture.TwoLetterISOLanguageName.ToLowerInvariant();
                return langCode switch
                {
                    "ru" => "Russian",
                    "en" => "English",
                    "zh" => "Chinese",
                    "ja" => "Japanese",
                    "ko" => "Korean",
                    _ => "Auto"
                };
            }
            catch
            {
                return "Auto";
            }
        }
    }

    public class ModelSettings
    {
        public ModelCoreSettings Core { get; set; } = new();
        public AutoUnloadSettings AutoUnload { get; set; } = new();
        public OptimizationSettings Optimizations { get; set; } = new();
        public WarmupSettings Warmup { get; set; } = new();
    }

    public class ModelCoreSettings
    {
        public string Name { get; set; } = "0.6B";
        public string Attention { get; set; } = "auto";
        public string Quantization { get; set; } = "none";
        public bool ForceCpu { get; set; } = false;
    }

    public class AutoUnloadSettings
    {
        public bool Enabled { get; set; } = false;
        public int Minutes { get; set; } = 15;
    }

    public class OptimizationSettings
    {
        public bool Enabled { get; set; } = true;
        public bool TorchCompile { get; set; } = true;
        public bool CudaGraphs { get; set; } = false;
        public bool CompileCodebook { get; set; } = true;
        public bool FastCodebook { get; set; } = true;
    }

    public class WarmupSettings
    {
        public string Mode { get; set; } = "none";
        public string Language { get; set; } = GetDefaultWarmupLanguage();
        public string Voice { get; set; } = "";
        public int TimeoutSeconds { get; set; } = 240;

        private static string GetDefaultWarmupLanguage()
        {
            try
            {
                var culture = CultureInfo.CurrentUICulture;
                var langCode = culture.TwoLetterISOLanguageName.ToLowerInvariant();
                return langCode switch
                {
                    "ru" => "Russian",
                    "en" => "English",
                    "zh" => "Chinese",
                    "ja" => "Japanese",
                    "ko" => "Korean",
                    _ => "Auto"
                };
            }
            catch
            {
                return "Auto";
            }
        }
    }

    public class InferenceSettings
    {
        public bool DoSample { get; set; } = true;
        public int MaxBatchSize { get; set; } = 1;
    }

    public class CacheSettings
    {
        public int LimitMB { get; set; } = 150;
    }

    public class HotkeySettings
    {
        public bool Enabled { get; set; } = true;
        public string SkipCurrentKey { get; set; } = "VcNumPad5";
        public string SkipAllKey { get; set; } = "VcNumPad4";
    }

    public class UiSettings
    {
        public int ActiveServiceTab { get; set; } = 0;
        public bool ServicesExpanded { get; set; } = true;
        public bool VoiceSettingsExpanded { get; set; } = true;
        public bool ModelControlExpanded { get; set; } = true;
        public bool QueueExpanded { get; set; } = true;
        public bool CacheExpanded { get; set; } = true;
        public bool SettingsExpanded { get; set; } = true;
        public bool IsQueuePanelOpen { get; set; } = false;
    }

    public class MetadataSettings
    {
        private const int LatestVersion = 1;

        public int SettingsVersion { get; set; } = 0;
        public bool HasCompletedWizard { get; set; } = false;
        public string LanguageUI { get; set; } = "en";
        public DateTime? LastUpdateCheck { get; set; }
        public string? SkippedClientVersion { get; set; }
        public string? SkippedServerVersion { get; set; }
    }
}
