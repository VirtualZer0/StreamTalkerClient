using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Managers;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;

namespace StreamTalkerClient.ViewModels;

/// <summary>
/// Primary ViewModel for the main application window. Orchestrates all services,
/// manages application state, and drives the chat-to-TTS pipeline.
///
/// This is a partial class split across multiple files:
/// <list type="bullet">
///   <item><description>MainWindowViewModel.cs — Constructor, fields, service accessors, Dispose</description></item>
///   <item><description>MainWindowViewModel.Properties.cs — All [ObservableProperty] declarations</description></item>
///   <item><description>MainWindowViewModel.EventHandlers.cs — Event handler setup methods</description></item>
///   <item><description>MainWindowViewModel.Commands.cs — All [RelayCommand] methods</description></item>
///   <item><description>MainWindowViewModel.PropertyChangedHandlers.cs — All partial void OnXxxChanged() methods</description></item>
///   <item><description>MainWindowViewModel.StatusDisplay.cs — UI update and status display helpers</description></item>
///   <item><description>MainWindowViewModel.Settings.cs — Settings load/save, reward caching, initial data load</description></item>
///   <item><description>MainWindowViewModel.Timers.cs — Timer setup and callback methods</description></item>
/// </list>
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    // ───────────────────────── Logger ─────────────────────────
    private readonly ILogger<MainWindowViewModel> _logger;

    // ───────────────────────── Services ─────────────────────────
    private readonly AppSettings _settings;
    private readonly QwenTtsClient _ttsClient;
    private readonly TwitchService _twitchService;
    private readonly VKPlayService _vkPlayService;
    private readonly MessageQueueManager _queueManager;
    private readonly SynthesisOrchestrator _orchestrator;
    private readonly PlaybackController _playbackController;
    private readonly AudioPlayer _audioPlayer;
    private readonly CacheManager _cacheManager;
    private readonly TtsConnectionManager _ttsConnectionManager;
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly MessageFilterService _messageFilterService;
    private readonly UpdateService _updateService;

    // ───────────────────────── Timers ─────────────────────────
    private DispatcherTimer? _uiUpdateTimer;
    private DispatcherTimer? _autoSaveTimer;
    private DispatcherTimer? _gpuUpdateTimer;

    // ───────────────────────── State flags ─────────────────────────
    private bool _isInitialized;
    private bool _suppressModelSwitch;
    private bool _disposed;
    private ModelsStatusResponse? _lastModelsStatus;
    private CancellationTokenSource? _vramDebounceCts;
    private string _rawModelServerStatus = "unloaded";
    private CancellationTokenSource? _synthDebounceCts;

    // ───────────────────────── VRAM notification flags ─────────────────────────
    private bool _vramLimitExceededNotified;
    private long _vramLimitExceededNotificationId;
    private bool _vramHighUsageNotified;
    private long _vramHighUsageNotificationId;

    // ───────────────────────── Change detection for UI updates ─────────────────────────
    private int _lastCacheItemCount = -1;
    private long _lastCacheSizeBytes = -1;
    private string _lastQueueFingerprint = "";

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC SERVICE ACCESSORS (for View code-behind dialogs)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Exposes the TTS client so the View code-behind can create dialog windows
    /// (e.g. ManageVoicesWindow) that need direct server access.
    /// </summary>
    public QwenTtsClient TtsClient => _ttsClient;

    /// <summary>
    /// Exposes the application settings so the View code-behind can pass them
    /// to dialog windows (e.g. VoiceBindingsWindow).
    /// </summary>
    public AppSettings Settings => _settings;

    /// <summary>
    /// Exposes the queue manager for dialogs that need queue access.
    /// </summary>
    public MessageQueueManager QueueManager => _queueManager;

    /// <summary>
    /// Exposes the update service for the View to create the update dialog.
    /// </summary>
    public UpdateService UpdateService => _updateService;

    /// <summary>
    /// Raised when an update is available and the dialog should be shown.
    /// The event argument contains the UpdateInfo to display.
    /// </summary>
    public event Action<Models.UpdateInfo>? ShowUpdateRequested;
    public event Action<Models.ServerUpdateInfo>? ShowServerUpdateRequested;

    // ═══════════════════════════════════════════════════════════
    //  CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Initializes the MainWindowViewModel: creates all services, loads settings,
    /// wires up event handlers, starts background services and timers,
    /// and kicks off the initial data load from the TTS server.
    /// </summary>
    public MainWindowViewModel()
    {
        _logger = AppLoggerFactory.CreateLogger<MainWindowViewModel>();
        _logger.LogInformation("MainWindowViewModel initializing...");

        // 1. Load settings
        _settings = SettingsRepository.Load();

        // 2. Create services
        _ttsClient = new QwenTtsClient(_settings.Server.BaseUrl);
        _twitchService = new TwitchService();
        _vkPlayService = new VKPlayService();
        _audioPlayer = new AudioPlayer();
        _cacheManager = new CacheManager(AppSettings.CacheFolder, _settings.Cache.LimitMB);
        _queueManager = new MessageQueueManager();
        _playbackController = new PlaybackController(_audioPlayer, _queueManager, _cacheManager, _settings);

        _orchestrator = new SynthesisOrchestrator(
            _ttsClient,
            _queueManager,
            _cacheManager,
            _playbackController);

        _ttsConnectionManager = new TtsConnectionManager(
            _ttsClient,
            TimeSpan.FromSeconds(AppConstants.Intervals.ModelStatusCheckSeconds));

        _hotkeyService = new GlobalHotkeyService();
        _messageFilterService = new MessageFilterService(_settings, _queueManager);
        _updateService = new UpdateService();

        // 3. Load settings into properties (before event wiring to avoid feedback)
        LoadSettingsToProperties();

        // 4. Set up event handlers
        SetupStreamingEventHandlers();
        SetupTtsConnectionEvents();
        SetupQueueEvents();
        SetupOrchestratorEvents();
        SetupHotkeyEvents();

        // 5. Start background services
        _orchestrator.Start();
        _ttsConnectionManager.Start();
        _hotkeyService.Start();

        // 6. Start timers
        StartTimers();

        // 7. Kick off initial data load (async) and mark initialization complete afterward
        _ = Task.Run(async () =>
        {
            try
            {
                await InitialDataLoadAsync();
            }
            finally
            {
                // Mark initialization complete after data load finishes
                _isInitialized = true;
            }

            // 8. Delayed update check
            await CheckForUpdateOnStartupAsync();
        });

        StatusBarText = GetLocalizedString("ReadyStatus", "Ready");

        // 9. Initialize all status display fields with localized values
        RefreshAllStatusDisplays();

        _logger.LogInformation("MainWindowViewModel initialized successfully");
    }

    // ═══════════════════════════════════════════════════════════
    //  CLEANUP / DISPOSE
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Performs graceful cleanup: stops timers, saves settings, stops background
    /// services, disconnects streaming platforms, and clears the message queue.
    /// </summary>
    public void Cleanup()
    {
        _logger.LogInformation("MainWindowViewModel cleanup starting...");

        // Stop timers
        _uiUpdateTimer?.Stop();
        _autoSaveTimer?.Stop();
        _gpuUpdateTimer?.Stop();

        // Save settings one final time
        SaveSettings();

        // Unload model if loaded
        if (IsModelLoaded && !string.IsNullOrEmpty(SelectedModel))
        {
            try
            {
                _logger.LogInformation("Unloading model {Model} before shutdown...", SelectedModel);
                var unloadTask = _ttsClient.UnloadModelAsync(SelectedModel);
                if (!unloadTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    _logger.LogWarning("Model unload timed out after 5 seconds");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error unloading model during cleanup");
            }
        }

        // Stop orchestrator (will stop synthesis and playback loops)
        _orchestrator.RequestStop();

        // Stop playback
        try
        {
            _playbackController.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping playback during cleanup");
        }

        // Disconnect streaming services
        try
        {
            _twitchService.Disconnect();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disconnecting Twitch during cleanup");
        }

        try
        {
            _vkPlayService.Disconnect();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disconnecting VK Play during cleanup");
        }

        // Stop TTS connection manager
        _ttsConnectionManager.Stop();

        // Stop global hotkeys
        _hotkeyService.Stop();

        // Clear queue
        _queueManager.Clear();

        _logger.LogInformation("MainWindowViewModel cleanup completed");
    }

    /// <summary>
    /// Disposes all managed resources. Calls <see cref="Cleanup"/> first,
    /// then disposes all service instances.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        Cleanup();

        _vramDebounceCts?.Cancel();
        _vramDebounceCts?.Dispose();
        _synthDebounceCts?.Cancel();
        _synthDebounceCts?.Dispose();
        _orchestrator.Dispose();
        _playbackController.Dispose();
        _audioPlayer.Dispose();
        _cacheManager.Dispose();
        _queueManager.Dispose();
        _twitchService.Dispose();
        _vkPlayService.Dispose();
        _ttsClient.Dispose();
        _ttsConnectionManager.Dispose();
        _hotkeyService.Dispose();
        _updateService.Dispose();

        GC.SuppressFinalize(this);
    }
}
