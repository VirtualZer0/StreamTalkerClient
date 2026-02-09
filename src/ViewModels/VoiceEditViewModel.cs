using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;

namespace StreamTalkerClient.ViewModels;

/// <summary>
/// ViewModel for the Voice create/edit dialog.
/// Used in both "create new voice" and "edit existing voice" modes.
/// </summary>
public partial class VoiceEditViewModel : ViewModelBase
{
    private readonly ILogger<VoiceEditViewModel> _logger;
    private readonly QwenTtsClient _ttsClient;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private string _voiceName = "";

    private string _originalVoiceName = "";

    [ObservableProperty]
    private string _selectedFilePath = "";

    [ObservableProperty]
    private string _filePathText = "";

    [ObservableProperty]
    private string _transcription = "";

    [ObservableProperty]
    private bool _overwriteVoice;

    [ObservableProperty]
    private bool _disableTranscription;

    [ObservableProperty]
    private double _voiceVolume = 100;

    [ObservableProperty]
    private string _volumeText = "100%";

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private bool _copiedVisible;

    public bool IsCreateMode => !IsEditMode;

    public event Action? CloseRequested;
    public bool WasModified { get; private set; }

    /// <summary>
    /// Constructor for CREATE mode.
    /// </summary>
    public VoiceEditViewModel(QwenTtsClient ttsClient, AppSettings settings)
    {
        _logger = AppLoggerFactory.CreateLogger<VoiceEditViewModel>();
        _ttsClient = ttsClient;
        _settings = settings;
        IsEditMode = false;
        FilePathText = DialogService.GetResource("FileNotSelected");
    }

    /// <summary>
    /// Constructor for EDIT mode (pre-populated with existing voice data).
    /// </summary>
    public VoiceEditViewModel(QwenTtsClient ttsClient, AppSettings settings, VoiceDisplayItem voice)
        : this(ttsClient, settings)
    {
        IsEditMode = true;
        _originalVoiceName = voice.Name;
        VoiceName = voice.Name;
        Transcription = voice.Transcription;
        VoiceVolume = voice.Volume;
        VolumeText = $"{voice.Volume}%";
    }

    partial void OnVoiceVolumeChanged(double value)
    {
        var volumeInt = (int)Math.Round(value);
        VolumeText = $"{volumeInt}%";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsEditMode)
        {
            await SaveEditAsync();
        }
        else
        {
            await CreateVoiceAsync();
        }
    }

    private async Task CreateVoiceAsync()
    {
        var name = VoiceName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), DialogService.GetResource("EnterVoiceName"));
            return;
        }

        if (!IsValidVoiceName(name))
        {
            await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), DialogService.GetResource("InvalidVoiceName"));
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), DialogService.GetResource("SelectAudioFile"));
            return;
        }

        IsCreating = true;
        try
        {
            var transcriptionText = string.IsNullOrWhiteSpace(Transcription) ? null : Transcription.Trim();
            var (success, message) = await _ttsClient.CreateVoiceAsync(
                name, SelectedFilePath, transcriptionText,
                overwrite: OverwriteVoice,
                disableTranscription: DisableTranscription);

            if (success)
            {
                _logger.LogInformation("Voice '{VoiceName}' created successfully", name);

                // Set volume for new voice
                var volumeInt = (int)Math.Round(VoiceVolume);
                if (volumeInt != 100)
                {
                    _settings.Audio.SetVoiceVolume(name, volumeInt);
                    _settings.Save();
                }

                WasModified = true;
                await DialogService.ShowMessageAsync(DialogService.GetResource("SuccessTitle"), message);
                CloseRequested?.Invoke();
            }
            else
            {
                await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating voice '{VoiceName}'", name);
            await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), ex.Message);
        }
        finally
        {
            IsCreating = false;
        }
    }

    private async Task SaveEditAsync()
    {
        var newName = VoiceName?.Trim() ?? "";
        var volumeInt = (int)Math.Round(VoiceVolume);

        // Rename if name changed
        if (!string.IsNullOrEmpty(newName) && newName != _originalVoiceName)
        {
            if (!IsValidVoiceName(newName))
            {
                await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), DialogService.GetResource("InvalidVoiceName"));
                return;
            }

            var (success, message) = await _ttsClient.RenameVoiceAsync(_originalVoiceName, newName);
            if (!success)
            {
                await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), message);
                return;
            }

            // Migrate volume setting to new name
            if (_settings.Audio.VoiceVolumes.ContainsKey(_originalVoiceName))
            {
                _settings.Audio.VoiceVolumes.Remove(_originalVoiceName);
            }

            _logger.LogInformation("Voice '{OldName}' renamed to '{NewName}'", _originalVoiceName, newName);
        }

        // Save volume
        _settings.Audio.SetVoiceVolume(newName, volumeInt);
        _settings.Save();
        WasModified = true;

        _logger.LogInformation("Voice '{VoiceName}' settings saved (volume: {Volume}%)", newName, volumeInt);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private async Task CopyTranscription()
    {
        if (!string.IsNullOrWhiteSpace(Transcription))
        {
            try
            {
                var clipboard = Avalonia.Application.Current?.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow?.Clipboard : null;

                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(Transcription);
                    CopiedVisible = true;
                    _ = HideCopiedAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy transcription to clipboard");
            }
        }
    }

    private async Task HideCopiedAsync()
    {
        await Task.Delay(2000);
        CopiedVisible = false;
    }

    /// <summary>
    /// Opens a file picker dialog to select an audio file.
    /// Must be called from code-behind, passing the parent window.
    /// </summary>
    public async Task BrowseFileAsync(Window window)
    {
        try
        {
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = DialogService.GetResource("AudioFileLabel"),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Audio files")
                    {
                        Patterns = new[] { "*.wav", "*.mp3", "*.flac", "*.ogg" }
                    },
                    FilePickerFileTypes.All
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                var path = file.TryGetLocalPath();
                if (!string.IsNullOrEmpty(path))
                {
                    SelectedFilePath = path;
                    FilePathText = System.IO.Path.GetFileName(path);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing for audio file");
        }
    }

    private static bool IsValidVoiceName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        return !name.Any(c => invalidChars.Contains(c));
    }
}
