using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.ViewModels;

public partial class ServerUpdateDialogViewModel : ViewModelBase
{
    private readonly ServerUpdateInfo _updateInfo;
    private readonly ILogger<ServerUpdateDialogViewModel> _logger;

    public event Action? CloseRequested;
    public event Action<string>? SkipVersionRequested;

    [ObservableProperty]
    private string _currentVersion;

    [ObservableProperty]
    private string _newVersion;

    [ObservableProperty]
    private string _changelog;

    [ObservableProperty]
    private string _releaseUrl;

    [ObservableProperty]
    private bool _isLocalServer;

    [ObservableProperty]
    private bool _isUpdating;

    [ObservableProperty]
    private string _updateOutputText = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _updateComplete;

    private readonly string _dockerComposeDir;

    public string DockerCommand => "docker compose up -d --pull always";

    public ServerUpdateDialogViewModel(ServerUpdateInfo updateInfo)
    {
        _updateInfo = updateInfo;
        _logger = AppLoggerFactory.CreateLogger<ServerUpdateDialogViewModel>();

        _currentVersion = updateInfo.CurrentVersion;
        _newVersion = updateInfo.NewVersion;
        _changelog = updateInfo.Changelog;
        _releaseUrl = updateInfo.ReleaseUrl;
        _isLocalServer = updateInfo.IsLocalServer;
        _dockerComposeDir = updateInfo.DockerComposeDir;
    }

    [RelayCommand]
    private async Task UpdateNowAsync()
    {
        if (IsUpdating) return;

        IsUpdating = true;
        HasError = false;
        ErrorMessage = "";
        UpdateOutputText = "";

        try
        {
            var composePath = Path.Combine(_dockerComposeDir, "docker-compose.yml");
            if (!File.Exists(composePath))
            {
                HasError = true;
                ErrorMessage = $"docker-compose.yml not found at {composePath}";
                IsUpdating = false;
                return;
            }

            ProcessStartInfo psi;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c docker compose -f \"{composePath}\" up -d --pull always",
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"docker compose -f '{composePath}' up -d --pull always\"",
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                HasError = true;
                ErrorMessage = "Failed to start docker process";
                IsUpdating = false;
                return;
            }

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                UpdateComplete = true;
                _logger.LogInformation("Server update completed successfully");
            }
            else
            {
                HasError = true;
                ErrorMessage = GetLocalizedString("ServerUpdateFailed", "Server update failed");
                _logger.LogWarning("Server update failed with exit code {ExitCode}", process.ExitCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Server update failed");
            HasError = true;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsUpdating = false;
        }
    }

    [RelayCommand]
    private async Task CopyCommandAsync()
    {
        try
        {
            var clipboard = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
                ?.MainWindow?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(DockerCommand);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to copy to clipboard");
        }
    }

    [RelayCommand]
    private void SkipThisVersion()
    {
        SkipVersionRequested?.Invoke(_updateInfo.NewVersion);
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenReleasePage()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _updateInfo.ReleaseUrl,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open release page");
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke();
    }

    private static string GetLocalizedString(string key, string fallback = "")
    {
        if (Application.Current != null &&
            Application.Current.TryGetResource(key,
                Application.Current.ActualThemeVariant, out var resource) &&
            resource is string str)
        {
            return str;
        }
        return fallback;
    }
}
