using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;

namespace StreamTalkerClient.ViewModels;

/// <summary>
/// Display model for a voice row in the DataGrid.
/// </summary>
public partial class VoiceDisplayItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _transcription = "";

    [ObservableProperty]
    private string _createdAt = "";

    [ObservableProperty]
    private string _volumeText = "100%";

    [ObservableProperty]
    private int _volume = 100;

    [ObservableProperty]
    private bool _hasCached06B;

    [ObservableProperty]
    private bool _hasCached17B;

    /// <summary>
    /// Reference to the underlying API voice info.
    /// </summary>
    public ApiVoiceInfo? Source { get; set; }
}

/// <summary>
/// ViewModel for the Voice Management dialog.
/// Manages voice listing, deletion, and signals for opening create/edit dialogs.
/// </summary>
public partial class VoiceManagementViewModel : ViewModelBase
{
    private readonly ILogger<VoiceManagementViewModel> _logger;
    private readonly QwenTtsClient _ttsClient;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private ObservableCollection<VoiceDisplayItem> _voices = new();

    [ObservableProperty]
    private VoiceDisplayItem? _selectedVoice;

    [ObservableProperty]
    private int _selectedVoiceIndex = -1;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _voicesModified;

    /// <summary>
    /// Event raised when the dialog should be closed.
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Event raised when the edit dialog should be opened for a voice.
    /// The code-behind subscribes to this to open the VoiceEditWindow.
    /// </summary>
    public event Func<VoiceDisplayItem, Task<bool>>? EditVoiceRequested;

    /// <summary>
    /// Event raised when the create dialog should be opened.
    /// </summary>
    public event Func<Task<bool>>? AddVoiceRequested;

    public QwenTtsClient TtsClient => _ttsClient;
    public AppSettings Settings => _settings;

    public VoiceManagementViewModel(QwenTtsClient ttsClient, AppSettings settings)
    {
        _logger = AppLoggerFactory.CreateLogger<VoiceManagementViewModel>();
        _ttsClient = ttsClient;
        _settings = settings;
    }

    /// <summary>
    /// Loads voices asynchronously. Call after construction.
    /// </summary>
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await LoadVoicesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize VoiceManagementViewModel");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadVoicesAsync()
    {
        try
        {
            var voiceList = await _ttsClient.GetVoicesDetailedAsync();
            Voices.Clear();

            foreach (var voice in voiceList)
            {
                var name = voice.Name ?? "";
                var volume = _settings.Audio.GetVoiceVolume(name);

                Voices.Add(new VoiceDisplayItem
                {
                    Name = name,
                    Transcription = voice.Transcription ?? "",
                    CreatedAt = FormatCreatedAt(voice.CreatedAt),
                    Volume = volume,
                    VolumeText = $"{volume}%",
                    HasCached06B = voice.CachedModels?.Contains("0.6B") == true,
                    HasCached17B = voice.CachedModels?.Contains("1.7B") == true,
                    Source = voice
                });
            }

            _logger.LogDebug("Loaded {Count} voices into management view", Voices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load voices");
            await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), DialogService.GetResource("FailedToLoadVoices"));
        }
    }

    private static string FormatCreatedAt(string? createdAt)
    {
        if (string.IsNullOrWhiteSpace(createdAt)) return "";

        if (DateTime.TryParse(createdAt, out var dt))
        {
            return dt.ToString("yyyy-MM-dd HH:mm");
        }
        return createdAt;
    }

    [RelayCommand]
    private async Task EditVoice(VoiceDisplayItem? voice)
    {
        if (voice == null) return;

        if (EditVoiceRequested != null)
        {
            var modified = await EditVoiceRequested(voice);
            if (modified)
            {
                VoicesModified = true;
                await LoadVoicesAsync();
            }
        }
    }

    [RelayCommand]
    private async Task AddNewVoice()
    {
        if (AddVoiceRequested != null)
        {
            var modified = await AddVoiceRequested();
            if (modified)
            {
                VoicesModified = true;
                await LoadVoicesAsync();
            }
        }
    }

    [RelayCommand]
    private async Task DeleteVoice(VoiceDisplayItem? voice)
    {
        if (voice == null) return;

        var voiceName = voice.Name;
        var confirmMessage = string.Format(DialogService.GetResource("ConfirmDeleteVoice"), voiceName);
        var confirmed = await DialogService.ShowConfirmAsync(DialogService.GetResource("ConfirmDeleteTitle"), confirmMessage);
        if (!confirmed) return;

        IsLoading = true;
        try
        {
            var (success, message) = await _ttsClient.DeleteVoiceAsync(voiceName);

            if (success)
            {
                _logger.LogInformation("Voice '{VoiceName}' deleted successfully", voiceName);

                if (_settings.Audio.VoiceVolumes.ContainsKey(voiceName))
                {
                    _settings.Audio.VoiceVolumes.Remove(voiceName);
                    _settings.Save();
                }

                VoicesModified = true;
                await LoadVoicesAsync();
            }
            else
            {
                await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting voice '{VoiceName}'", voiceName);
            await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearVoiceCache(VoiceDisplayItem? voice)
    {
        if (voice == null) return;

        var voiceName = voice.Name;
        var confirmMessage = string.Format(DialogService.GetResource("ConfirmClearVoiceCache"), voiceName);
        var confirmed = await DialogService.ShowConfirmAsync(DialogService.GetResource("ConfirmTitle"), confirmMessage);
        if (!confirmed) return;

        IsLoading = true;
        try
        {
            var (success, message) = await _ttsClient.ClearVoiceCacheAsync(voiceName);

            if (success)
            {
                _logger.LogInformation("Cache cleared for voice '{VoiceName}'", voiceName);
                await LoadVoicesAsync();
                await DialogService.ShowMessageAsync(DialogService.GetResource("SuccessTitle"), message);
            }
            else
            {
                await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache for voice '{VoiceName}'", voiceName);
            await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ClearAllVoiceCache()
    {
        var confirmMessage = DialogService.GetResource("ConfirmClearAllVoiceCache");
        var confirmed = await DialogService.ShowConfirmAsync(DialogService.GetResource("ConfirmTitle"), confirmMessage);
        if (!confirmed) return;

        IsLoading = true;
        try
        {
            var (success, message) = await _ttsClient.ClearAllVoiceCacheAsync();

            if (success)
            {
                _logger.LogInformation("Cache cleared for all voices");
                await LoadVoicesAsync();
                await DialogService.ShowMessageAsync(DialogService.GetResource("SuccessTitle"), message);
            }
            else
            {
                await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache for all voices");
            await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke();
    }
}
