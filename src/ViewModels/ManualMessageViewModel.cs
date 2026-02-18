using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Managers;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;
using System.Collections.ObjectModel;

namespace StreamTalkerClient.ViewModels;

/// <summary>
/// ViewModel for the manual message addition dialog.
/// Allows users to manually add messages to the synthesis queue with custom parameters.
/// </summary>
public partial class ManualMessageViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly MessageQueueManager _queueManager;
    private readonly ILogger<ManualMessageViewModel> _logger;

    // ═══════════════════════════════════════════════════════════
    //  OBSERVABLE PROPERTIES
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _messageText = "";

    [ObservableProperty]
    private VoiceInfo? _selectedVoice;

    [ObservableProperty]
    private string _selectedLanguage = "";

    [ObservableProperty]
    private double _speed = 1.0;

    [ObservableProperty]
    private double _temperature = 0.7;

    [ObservableProperty]
    private bool _doSample = true;

    [ObservableProperty]
    private int _maxNewTokens = 2000;

    [ObservableProperty]
    private double _repetitionPenalty = 1.05;

    [ObservableProperty]
    private bool _canAdd = false;

    // ═══════════════════════════════════════════════════════════
    //  COLLECTIONS
    // ═══════════════════════════════════════════════════════════

    public ObservableCollection<VoiceInfo> AvailableVoices { get; } = new();
    public ObservableCollection<string> AvailableLanguages { get; } = new();

    // ═══════════════════════════════════════════════════════════
    //  EVENTS
    // ═══════════════════════════════════════════════════════════

    /// <summary>Invoked when the dialog should close.</summary>
    public event Action? CloseRequested;

    /// <summary>Invoked when a message was successfully added to the queue.</summary>
    public event Action? MessageAdded;

    // ═══════════════════════════════════════════════════════════
    //  CONSTRUCTOR
    // ═══════════════════════════════════════════════════════════

    public ManualMessageViewModel(
        AppSettings settings,
        MessageQueueManager queueManager,
        IEnumerable<VoiceInfo> availableVoices)
    {
        _settings = settings;
        _queueManager = queueManager;
        _logger = AppLoggerFactory.CreateLogger<ManualMessageViewModel>();

        // Populate languages from single source of truth
        foreach (var lang in Infrastructure.AppConstants.Options.TtsLanguages)
            AvailableLanguages.Add(lang);

        // Populate voices
        foreach (var voice in availableVoices)
        {
            AvailableVoices.Add(voice);
        }

        // Load defaults from settings
        LoadDefaults();

        // Set up property change handlers
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MessageText) || e.PropertyName == nameof(SelectedVoice))
            {
                UpdateCanAdd();
            }
        };

        UpdateCanAdd();
    }

    // ═══════════════════════════════════════════════════════════
    //  INITIALIZATION
    // ═══════════════════════════════════════════════════════════

    private void LoadDefaults()
    {
        // Load synthesis parameters from current settings
        SelectedLanguage = _settings.Server.Language;
        Speed = _settings.Voice.Speed;
        Temperature = _settings.Voice.Temperature;
        DoSample = _settings.Inference.DoSample;
        MaxNewTokens = _settings.Voice.MaxNewTokens;
        RepetitionPenalty = _settings.Voice.RepetitionPenalty;

        // Select default voice
        if (!string.IsNullOrEmpty(_settings.Voice.DefaultVoice))
        {
            SelectedVoice = AvailableVoices.FirstOrDefault(v =>
                string.Equals(v.Name, _settings.Voice.DefaultVoice, StringComparison.OrdinalIgnoreCase));
        }

        // If no voice selected and voices available, select first
        if (SelectedVoice == null && AvailableVoices.Count > 0)
        {
            SelectedVoice = AvailableVoices[0];
        }
    }

    private void UpdateCanAdd()
    {
        CanAdd = !string.IsNullOrWhiteSpace(MessageText) && SelectedVoice != null;
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMANDS
    // ═══════════════════════════════════════════════════════════

    [RelayCommand]
    private void AddMessage()
    {
        if (!CanAdd || SelectedVoice == null)
            return;

        try
        {
            var text = MessageText.Trim();
            var voiceName = SelectedVoice.Name;

            // Add message to queue with custom parameters
            _queueManager.AddManualMessage(
                text: text,
                voiceName: voiceName,
                language: SelectedLanguage,
                speed: Speed,
                temperature: Temperature,
                maxNewTokens: MaxNewTokens,
                repetitionPenalty: RepetitionPenalty);

            _logger.LogInformation(
                "Manual message added to queue: voice={Voice}, length={Length}, params=[speed={Speed}, temp={Temp}, doSample={DoSample}]",
                voiceName, text.Length, Speed, Temperature, DoSample);

            // Notify and close
            MessageAdded?.Invoke();
            CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add manual message to queue");
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        LoadDefaults();
        MessageText = "";
    }
}
