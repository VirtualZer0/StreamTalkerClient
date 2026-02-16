using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;

namespace StreamTalkerClient.ViewModels;

/// <summary>
/// Display model for a voice binding row in the DataGrid.
/// </summary>
public partial class BindingDisplayItem : ObservableObject
{
    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _voiceName = "";

    [ObservableProperty]
    private string _platform = "Any";

    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>
    /// Reference to the underlying VoiceBinding model.
    /// </summary>
    public VoiceBinding Source { get; set; } = new();
}

/// <summary>
/// Display model for a blacklist entry row in the DataGrid.
/// </summary>
public partial class BlacklistDisplayItem : ObservableObject
{
    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _platform = "Any";

    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>
    /// Reference to the underlying BlacklistEntry model.
    /// </summary>
    public BlacklistEntry Source { get; set; } = new();
}

/// <summary>
/// ViewModel for the User Rules dialog (formerly Voice Binding dialog).
/// Manages voice-to-username bindings and blacklist entries with platform support.
/// </summary>
public partial class VoiceBindingViewModel : ViewModelBase
{
    private readonly ILogger<VoiceBindingViewModel> _logger;
    private readonly QwenTtsClient _ttsClient;
    private readonly AppSettings _settings;

    // ═══════════════════════════════════════════════════════════
    //  TAB STATE
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private int _selectedTabIndex;

    // ═══════════════════════════════════════════════════════════
    //  VOICE BINDINGS
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<BindingDisplayItem> _bindings = new();

    [ObservableProperty]
    private string _bindingsGroupText = "";

    [ObservableProperty]
    private ObservableCollection<string> _voices = new();

    [ObservableProperty]
    private int _selectedVoiceIndex = -1;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private BindingDisplayItem? _selectedBinding;

    [ObservableProperty]
    private int _selectedBindingIndex = -1;

    [ObservableProperty]
    private int _selectedBindingPlatformIndex; // 0=Any, 1=Twitch, 2=VK Play

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _canDelete;

    [ObservableProperty]
    private bool _canChangeVoice;

    [ObservableProperty]
    private bool _bindingsModified;

    // ═══════════════════════════════════════════════════════════
    //  BLACKLIST
    // ═══════════════════════════════════════════════════════════

    [ObservableProperty]
    private ObservableCollection<BlacklistDisplayItem> _blacklistEntries = new();

    [ObservableProperty]
    private string _blacklistGroupText = "";

    [ObservableProperty]
    private BlacklistDisplayItem? _selectedBlacklistEntry;

    [ObservableProperty]
    private int _selectedBlacklistIndex = -1;

    [ObservableProperty]
    private bool _canDeleteBlacklist;

    [ObservableProperty]
    private string _blacklistUsername = "";

    [ObservableProperty]
    private int _selectedBlacklistPlatformIndex; // 0=Any, 1=Twitch, 2=VK Play

    // ═══════════════════════════════════════════════════════════
    //  STATIC OPTIONS
    // ═══════════════════════════════════════════════════════════

    public static IReadOnlyList<string> PlatformOptions { get; } = new[] { "Any", "Twitch", "VK Play" };

    /// <summary>
    /// Event raised when the dialog should be closed.
    /// </summary>
    public event Action? CloseRequested;

    public VoiceBindingViewModel(QwenTtsClient ttsClient, AppSettings settings)
    {
        _logger = AppLoggerFactory.CreateLogger<VoiceBindingViewModel>();
        _ttsClient = ttsClient;
        _settings = settings;

        UpdateBindingsGroupText();
        UpdateBlacklistGroupText();
    }

    /// <summary>
    /// Loads voices, bindings, and blacklist asynchronously. Call after construction.
    /// </summary>
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await LoadVoicesAsync();
            LoadBindingsFromSettings();
            LoadBlacklistFromSettings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize VoiceBindingViewModel");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadVoicesAsync()
    {
        try
        {
            var voiceList = await _ttsClient.GetVoicesAsync();
            Voices.Clear();
            foreach (var voice in voiceList)
            {
                Voices.Add(voice.Name);
            }

            if (Voices.Count > 0)
            {
                SelectedVoiceIndex = 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load voices");
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  BINDINGS CRUD
    // ═══════════════════════════════════════════════════════════

    private void LoadBindingsFromSettings()
    {
        Bindings.Clear();
        foreach (var binding in _settings.Voice.VoiceBindings)
        {
            Bindings.Add(new BindingDisplayItem
            {
                Username = binding.Username,
                VoiceName = binding.VoiceName,
                Platform = binding.Platform,
                IsEnabled = binding.IsEnabled,
                Source = binding
            });
        }
        UpdateBindingsGroupText();
    }

    partial void OnSelectedBindingChanged(BindingDisplayItem? value)
    {
        UpdateSelectionState();
    }

    partial void OnSelectedBindingIndexChanged(int value)
    {
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        var hasSelection = SelectedBinding != null;
        CanDelete = hasSelection;
        CanChangeVoice = hasSelection && Voices.Count > 0;
    }

    private void UpdateBindingsGroupText()
    {
        BindingsGroupText = string.Format(
            DialogService.GetResource("BindingsCountFormat") ?? "Bindings ({0} items)",
            Bindings.Count);
    }

    private void SaveBindingsToSettings()
    {
        _settings.Voice.VoiceBindings.Clear();
        foreach (var item in Bindings)
        {
            _settings.Voice.VoiceBindings.Add(new VoiceBinding
            {
                Username = item.Username,
                VoiceName = item.VoiceName,
                Platform = item.Platform,
                IsEnabled = item.IsEnabled
            });
        }
        _settings.Save();
        BindingsModified = true;
        _logger.LogInformation("Voice bindings saved ({Count} bindings)", Bindings.Count);
    }

    private static string PlatformIndexToValue(int index)
    {
        return index switch
        {
            1 => "Twitch",
            2 => "VKPlay",
            _ => "Any"
        };
    }

    private static string PlatformValueToDisplay(string value)
    {
        return value switch
        {
            "VKPlay" => "VK Play",
            _ => value
        };
    }

    private static int PlatformValueToIndex(string value)
    {
        return value switch
        {
            "Twitch" => 1,
            "VKPlay" => 2,
            _ => 0
        };
    }

    [RelayCommand]
    private async Task AddBinding()
    {
        var username = Username?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), DialogService.GetResource("EnterUsername"));
            return;
        }

        if (SelectedVoiceIndex < 0 || SelectedVoiceIndex >= Voices.Count)
        {
            await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), DialogService.GetResource("SelectVoice"));
            return;
        }

        var voiceName = Voices[SelectedVoiceIndex];
        var platform = PlatformIndexToValue(SelectedBindingPlatformIndex);

        // Check if binding already exists for this username+platform
        var existingIndex = -1;
        for (int i = 0; i < Bindings.Count; i++)
        {
            if (string.Equals(Bindings[i].Username, username, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Bindings[i].Platform, platform, StringComparison.OrdinalIgnoreCase))
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            var confirmMessage = string.Format(DialogService.GetResource("ConfirmReplaceBinding"), username, voiceName);
            var confirmed = await DialogService.ShowConfirmAsync(DialogService.GetResource("ConfirmTitle"), confirmMessage);
            if (!confirmed) return;

            // Replace existing binding
            var existing = Bindings[existingIndex];
            existing.VoiceName = voiceName;
            existing.IsEnabled = true;
            existing.Source.VoiceName = voiceName;
            existing.Source.IsEnabled = true;
        }
        else
        {
            // Add new binding
            var binding = new VoiceBinding
            {
                Username = username,
                VoiceName = voiceName,
                Platform = platform,
                IsEnabled = true
            };

            Bindings.Add(new BindingDisplayItem
            {
                Username = username,
                VoiceName = voiceName,
                Platform = platform,
                IsEnabled = true,
                Source = binding
            });
        }

        Username = "";
        SaveBindingsToSettings();
        UpdateBindingsGroupText();
    }

    [RelayCommand]
    private async Task DeleteBinding()
    {
        if (SelectedBinding == null) return;

        var confirmMessage = string.Format(DialogService.GetResource("ConfirmDeleteBinding"), SelectedBinding.Username);
        var confirmed = await DialogService.ShowConfirmAsync(DialogService.GetResource("ConfirmTitle"), confirmMessage);
        if (!confirmed) return;

        Bindings.Remove(SelectedBinding);
        SaveBindingsToSettings();
        UpdateBindingsGroupText();
    }

    [RelayCommand]
    private async Task ChangeVoice()
    {
        if (SelectedBinding == null || Voices.Count == 0) return;

        var newVoice = await ShowChangeVoiceDialogAsync(SelectedBinding.VoiceName);
        if (newVoice == null) return;

        SelectedBinding.VoiceName = newVoice;
        SelectedBinding.Source.VoiceName = newVoice;
        SaveBindingsToSettings();

        // Force refresh of the selected item in the grid
        var index = Bindings.IndexOf(SelectedBinding);
        if (index >= 0)
        {
            var item = Bindings[index];
            Bindings.RemoveAt(index);
            Bindings.Insert(index, item);
            SelectedBindingIndex = index;
        }
    }

    [RelayCommand]
    private void ToggleBinding(BindingDisplayItem? item)
    {
        if (item == null) return;

        item.IsEnabled = !item.IsEnabled;
        item.Source.IsEnabled = item.IsEnabled;
        SaveBindingsToSettings();
    }

    // ═══════════════════════════════════════════════════════════
    //  BLACKLIST CRUD
    // ═══════════════════════════════════════════════════════════

    private void LoadBlacklistFromSettings()
    {
        BlacklistEntries.Clear();
        foreach (var entry in _settings.Voice.Blacklist)
        {
            BlacklistEntries.Add(new BlacklistDisplayItem
            {
                Username = entry.Username,
                Platform = entry.Platform,
                IsEnabled = entry.IsEnabled,
                Source = entry
            });
        }
        UpdateBlacklistGroupText();
    }

    partial void OnSelectedBlacklistEntryChanged(BlacklistDisplayItem? value)
    {
        CanDeleteBlacklist = value != null;
    }

    partial void OnSelectedBlacklistIndexChanged(int value)
    {
        CanDeleteBlacklist = SelectedBlacklistEntry != null;
    }

    private void UpdateBlacklistGroupText()
    {
        BlacklistGroupText = string.Format(
            DialogService.GetResource("BlacklistCountFormat") ?? "Blacklist ({0} items)",
            BlacklistEntries.Count);
    }

    private void SaveBlacklistToSettings()
    {
        _settings.Voice.Blacklist.Clear();
        foreach (var item in BlacklistEntries)
        {
            _settings.Voice.Blacklist.Add(new BlacklistEntry
            {
                Username = item.Username,
                Platform = item.Platform,
                IsEnabled = item.IsEnabled
            });
        }
        _settings.Save();
        BindingsModified = true;
        _logger.LogInformation("Blacklist saved ({Count} entries)", BlacklistEntries.Count);
    }

    [RelayCommand]
    private async Task AddBlacklistEntry()
    {
        var username = BlacklistUsername?.Trim();
        if (string.IsNullOrWhiteSpace(username))
        {
            await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), DialogService.GetResource("EnterUsername"));
            return;
        }

        var platform = PlatformIndexToValue(SelectedBlacklistPlatformIndex);

        // Check duplicate (username+platform)
        var exists = BlacklistEntries.Any(b =>
            string.Equals(b.Username, username, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(b.Platform, platform, StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            await DialogService.ShowMessageAsync(DialogService.GetResource("ErrorTitle"), DialogService.GetResource("EnterUsername"));
            return;
        }

        var entry = new BlacklistEntry
        {
            Username = username,
            Platform = platform,
            IsEnabled = true
        };

        BlacklistEntries.Add(new BlacklistDisplayItem
        {
            Username = username,
            Platform = platform,
            IsEnabled = true,
            Source = entry
        });

        BlacklistUsername = "";
        SaveBlacklistToSettings();
        UpdateBlacklistGroupText();
    }

    [RelayCommand]
    private async Task DeleteBlacklistEntry()
    {
        if (SelectedBlacklistEntry == null) return;

        var confirmMessage = string.Format(
            DialogService.GetResource("ConfirmDeleteBlacklist") ?? "Remove \"{0}\" from blacklist?",
            SelectedBlacklistEntry.Username);
        var confirmed = await DialogService.ShowConfirmAsync(DialogService.GetResource("ConfirmTitle"), confirmMessage);
        if (!confirmed) return;

        BlacklistEntries.Remove(SelectedBlacklistEntry);
        SaveBlacklistToSettings();
        UpdateBlacklistGroupText();
    }

    [RelayCommand]
    private void ToggleBlacklistEntry(BlacklistDisplayItem? item)
    {
        if (item == null) return;

        item.IsEnabled = !item.IsEnabled;
        item.Source.IsEnabled = item.IsEnabled;
        SaveBlacklistToSettings();
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMON
    // ═══════════════════════════════════════════════════════════

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke();
    }

    /// <summary>
    /// Shows a change voice dialog and returns the selected voice name, or null if cancelled.
    /// </summary>
    private async Task<string?> ShowChangeVoiceDialogAsync(string currentVoice)
    {
        var mainWindow = DialogService.GetMainWindow();
        if (mainWindow == null) return null;

        var dialog = new Window
        {
            Title = DialogService.GetResource("ChangeVoiceTitle"),
            Width = 300,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };

        string? result = null;

        var comboBox = new ComboBox
        {
            ItemsSource = Voices,
            SelectedIndex = Voices.IndexOf(currentVoice),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Margin = new Avalonia.Thickness(10, 0, 10, 0)
        };

        var label = new TextBlock
        {
            Text = DialogService.GetResource("SelectVoiceLabel"),
            Margin = new Avalonia.Thickness(10, 10, 10, 5)
        };

        var okButton = new Button
        {
            Content = DialogService.GetResource("OkButton"),
            Width = 80,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 0, 5, 0)
        };
        okButton.Click += (_, _) =>
        {
            if (comboBox.SelectedIndex >= 0)
                result = Voices[comboBox.SelectedIndex];
            dialog.Close();
        };

        var cancelButton = new Button
        {
            Content = DialogService.GetResource("CancelButton"),
            Width = 80,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        cancelButton.Click += (_, _) => dialog.Close();

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(10, 15, 10, 10),
            Spacing = 5,
            Children = { okButton, cancelButton }
        };

        var panel = new StackPanel
        {
            Children = { label, comboBox, buttonPanel }
        };

        dialog.Content = panel;

        await dialog.ShowDialog(mainWindow);
        return result;
    }

}
