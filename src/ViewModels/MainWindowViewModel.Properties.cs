using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.ViewModels;

/// <summary>
/// Observable properties for MainWindowViewModel.
/// All [ObservableProperty] declarations are grouped by feature area.
/// </summary>
public partial class MainWindowViewModel
{
    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - Twitch
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _channel = "";

    [ObservableProperty]
    private StreamConnectionState _twitchConnectionState = StreamConnectionState.Disconnected;

    [ObservableProperty]
    private string _twitchStatusText = "";

    [ObservableProperty]
    private string _twitchConnectButtonText = "";

    [ObservableProperty]
    private string _twitchTabHeader = "";

    [ObservableProperty]
    private string _twitchTabTooltip = "";

    [ObservableProperty]
    private ObservableCollection<string> _rewards = new();

    [ObservableProperty]
    private int _selectedRewardIndex = -1;

    [ObservableProperty]
    private bool _readAllMessages;

    [ObservableProperty]
    private bool _requireVoice;

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - VK Play
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _vkChannel = "";

    [ObservableProperty]
    private StreamConnectionState _vkConnectionState = StreamConnectionState.Disconnected;

    [ObservableProperty]
    private string _vkStatusText = "";

    [ObservableProperty]
    private string _vkConnectButtonText = "";

    [ObservableProperty]
    private string _vkTabHeader = "";

    [ObservableProperty]
    private string _vkTabTooltip = "";

    [ObservableProperty]
    private ObservableCollection<string> _vkRewards = new();

    [ObservableProperty]
    private int _selectedVkRewardIndex = -1;

    [ObservableProperty]
    private bool _vkReadAllMessages;

    [ObservableProperty]
    private bool _vkRequireVoice;

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - Voice
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<VoiceInfo> _availableVoices = new();

    [ObservableProperty]
    private VoiceInfo? _selectedVoice;

    [ObservableProperty]
    private int _selectedExtractionModeIndex;

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - Model
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<string> _models = new();

    [ObservableProperty]
    private string? _selectedModel;

    private static readonly string[] ModelIndexMap = ["0.6B", "1.7B"];

    /// <summary>
    /// Gets or sets the selected model index for ComboBox binding.
    /// Maps between the UI index and the model name string.
    /// </summary>
    public int SelectedModelIndex
    {
        get => Array.IndexOf(ModelIndexMap, SelectedModel ?? "0.6B") is var i && i >= 0 ? i : 0;
        set
        {
            if (value >= 0 && value < ModelIndexMap.Length)
                SelectedModel = ModelIndexMap[value];
        }
    }

    [ObservableProperty]
    private string? _selectedAttention;

    [ObservableProperty]
    private string? _selectedQuantization;

    [ObservableProperty]
    private bool _doSample = true;

    [ObservableProperty]
    private string _modelStatusText = "";

    [ObservableProperty]
    private string _modelState = "unknown";

    [ObservableProperty]
    private bool _autoUnload;

    [ObservableProperty]
    private int _autoUnloadMinutes = 15;

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - GPU
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _gpuUsagePercent;

    [ObservableProperty]
    private string _gpuUsageText = "";

    [ObservableProperty]
    private int _maxVramSliderValue;

    [ObservableProperty]
    private int _maxVramSliderMax;

    [ObservableProperty]
    private string _maxVramText = "";

    [ObservableProperty]
    private double _gpuUsedMb;

    [ObservableProperty]
    private double _gpuTotalMb;

    [ObservableProperty]
    private double _vramLimitPercent = 100;

    [ObservableProperty]
    private bool _hasVramLimit;

    [ObservableProperty]
    private string _vramTooltipText = "";

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - Notifications
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<NotificationItem> _notifications = new();

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - Queue
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<QueueDisplayItem> _queueItems = new();

    [ObservableProperty]
    private string _queueGroupText = "";

    [ObservableProperty]
    private int _volume = 100;

    [ObservableProperty]
    private string _volumeText = "100%";

    [ObservableProperty]
    private string _hotkeysText = "";

    [ObservableProperty]
    private int _timeout;

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - Cache
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _cacheSizeText = "";

    [ObservableProperty]
    private int _cacheProgress;

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - Settings / Server
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _ttsServerUrl = "http://localhost:7860";

    [ObservableProperty]
    private string _ttsServerUrlEdit = "http://localhost:7860";

    [ObservableProperty]
    private string _serverStatusText = "";

    [ObservableProperty]
    private int _playbackDelay = 5;

    [ObservableProperty]
    private int _cacheLimitMB = 150;

    [ObservableProperty]
    private bool _isServerAvailable;

    [ObservableProperty]
    private string _serverState = "unavailable";

    [ObservableProperty]
    private int _selectedLanguageIndex;

    [ObservableProperty]
    private string _selectedTtsLanguage = "Auto";

    [ObservableProperty]
    private string _selectedWarmup = "none";

    [ObservableProperty]
    private bool _forceCpu;

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - Optimization Flags
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _enableOptimizations = true;

    [ObservableProperty]
    private bool _torchCompile = true;

    [ObservableProperty]
    private bool _cudaGraphs;

    [ObservableProperty]
    private bool _compileCodebook = true;

    [ObservableProperty]
    private bool _fastCodebook = true;

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - Warmup Sub-parameters
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _warmupLang = "";

    [ObservableProperty]
    private string _warmupVoice = "";

    [ObservableProperty]
    private VoiceInfo? _selectedWarmupVoice;

    [ObservableProperty]
    private int _warmupTimeout = 240;

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - Inference Parameters
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private double _speed = 1.0;

    [ObservableProperty]
    private double _temperature = 0.7;

    [ObservableProperty]
    private int _maxNewTokens = 2000;

    [ObservableProperty]
    private double _repetitionPenalty = 1.05;

    [ObservableProperty]
    private int _maxBatchSize = 2;

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - UI State
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _activeServiceTabIndex;

    [ObservableProperty]
    private bool _servicesExpanded = true;

    [ObservableProperty]
    private bool _voiceSettingsExpanded = true;

    [ObservableProperty]
    private bool _modelControlExpanded = true;

    [ObservableProperty]
    private bool _queueExpanded = true;

    [ObservableProperty]
    private bool _cacheExpanded = true;

    [ObservableProperty]
    private bool _settingsExpanded = true;

    [ObservableProperty]
    private bool _isQueuePanelOpen;

    [ObservableProperty]
    private bool _isSettingsWindowOpen;

    [ObservableProperty]
    private bool _isModelOperationInProgress;

    /// <summary>
    /// Returns true when the current model is loaded and ready on the TTS server.
    /// </summary>
    public bool IsModelLoaded => _rawModelServerStatus == "ready";

    // StatusBarText is kept for internal status tracking but no longer displayed in the main window UI
    [ObservableProperty]
    private string _statusBarText = "";

    /// <summary>
    /// Application version string for display in Settings.
    /// </summary>
    public string AppVersionText { get; } = $"v{Services.UpdateService.GetCurrentVersion()}";

#if DEBUG
    public bool IsDebugBuild => true;
#else
    public bool IsDebugBuild => false;
#endif

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES - Indicator
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private IndicatorState _indicatorState = IndicatorState.Unloaded;

    [ObservableProperty]
    private bool _isSynthesizing;

    // ═══════════════════════════════════════════════════════════
    //  STATIC OPTION LISTS
    // ═══════════════════════════════════════════════════════════

    public static IReadOnlyList<string> AttentionOptions { get; } = new[]
    {
        "auto", "sage_attn", "flex_attn", "flash2_attn", "sdpa", "eager"
    };

    public static IReadOnlyList<string> QuantizationOptions { get; } = new[]
    {
        "none", "int8", "float8"
    };

    public static IReadOnlyList<string> LanguageOptions { get; } = new[]
    {
        "English", "Russian"
    };

    public static IReadOnlyList<string> TtsLanguageOptions { get; } = new[]
    {
        "Auto", "Chinese", "English", "Japanese", "Korean", "French", "German",
        "Spanish", "Portuguese", "Russian", "Arabic", "Italian"
    };

    public static IReadOnlyList<string> WarmupOptions { get; } = new[]
    {
        "none", "single", "batch"
    };

    public static IReadOnlyList<string> ExtractionModeOptions { get; } = new[]
    {
        "bracket", "firstword"
    };
}
