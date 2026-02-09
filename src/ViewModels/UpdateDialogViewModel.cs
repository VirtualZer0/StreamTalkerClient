using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;

namespace StreamTalkerClient.ViewModels;

public partial class UpdateDialogViewModel : ViewModelBase
{
    private readonly UpdateService _updateService;
    private readonly UpdateInfo _updateInfo;
    private readonly ILogger<UpdateDialogViewModel> _logger;

    public event Action? CloseRequested;
    public event Action<string>? SkipVersionRequested;

    [ObservableProperty]
    private string _currentVersion;

    [ObservableProperty]
    private string _newVersion;

    [ObservableProperty]
    private string _changelog;

    [ObservableProperty]
    private string _assetName;

    [ObservableProperty]
    private string _assetSizeText;

    [ObservableProperty]
    private string _releaseUrl;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _hasError;

    public UpdateDialogViewModel(UpdateService updateService, UpdateInfo updateInfo)
    {
        _updateService = updateService;
        _updateInfo = updateInfo;
        _logger = AppLoggerFactory.CreateLogger<UpdateDialogViewModel>();

        _currentVersion = updateInfo.CurrentVersion;
        _newVersion = updateInfo.NewVersion;
        _changelog = updateInfo.Changelog;
        _assetName = updateInfo.AssetName;
        _releaseUrl = updateInfo.ReleaseUrl;
        _assetSizeText = FormatFileSize(updateInfo.AssetSize);
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (IsDownloading || IsInstalling)
            return;

        IsDownloading = true;
        HasError = false;
        ErrorMessage = "";
        DownloadProgress = 0;

        try
        {
            var progress = new Progress<double>(p =>
            {
                DownloadProgress = p * 100;
            });

            var downloadedPath = await _updateService.DownloadUpdateAsync(_updateInfo, progress);

            if (downloadedPath == null)
            {
                IsDownloading = false;
                HasError = true;
                ErrorMessage = GetLocalizedString("UpdateDownloadFailed", "Download failed or was cancelled");
                return;
            }

            IsDownloading = false;
            IsInstalling = true;

            _updateService.ApplyUpdateAndRestart(downloadedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed");
            IsDownloading = false;
            IsInstalling = false;
            HasError = true;
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsDownloading)
        {
            _updateService.CancelDownload();
            IsDownloading = false;
            return;
        }

        CloseRequested?.Invoke();
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
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _updateInfo.ReleaseUrl,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open release page");
        }
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB",
            >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            >= 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }

    private static string GetLocalizedString(string key, string fallback = "")
    {
        if (Avalonia.Application.Current != null &&
            Avalonia.Application.Current.TryGetResource(key,
                Avalonia.Application.Current.ActualThemeVariant, out var resource) &&
            resource is string str)
        {
            return str;
        }
        return fallback;
    }
}
