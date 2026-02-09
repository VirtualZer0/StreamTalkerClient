using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;
using StreamTalkerClient.Services;

namespace StreamTalkerClient.ViewModels.Wizard;

/// <summary>
/// ViewModel for the first-launch wizard.
/// Orchestrates multi-step server setup flow.
/// </summary>
public partial class FirstLaunchWizardViewModel : ObservableObject
{
    private readonly ILogger<FirstLaunchWizardViewModel> _logger;
    private readonly AppSettings _settings;
    private readonly WizardPlatformService _platformService;

    [ObservableProperty] private WizardStep _currentStep = WizardStep.Welcome;
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _errorMessage = "";

    // Step 2.2 - Remote server URL
    [ObservableProperty] private string _serverUrl = "http://";

    // Step 4.1 - Resource limits
    [ObservableProperty] private int _memoryGb = 4;
    [ObservableProperty] private int _cpuPercent = 70;

    public FirstLaunchWizardViewModel()
    {
        _logger = AppLoggerFactory.CreateLogger<FirstLaunchWizardViewModel>();
        _settings = SettingsRepository.Load();
        _platformService = new WizardPlatformService(AppLoggerFactory.CreateLogger<WizardPlatformService>());
    }

    /// <summary>Navigate to a specific wizard step.</summary>
    [RelayCommand]
    private void GoToStep(WizardStep step)
    {
        CurrentStep = step;
        ErrorMessage = "";
        StatusMessage = "";
    }

    /// <summary>Step 2.1 - Check for local server at localhost:7860.</summary>
    [RelayCommand]
    private async Task CheckLocalServerAsync()
    {
        IsLoading = true;
        StatusMessage = LocalizationManager.Get("WizardCheckingConnection") ?? "Checking connection…";
        ErrorMessage = "";

        try
        {
            var client = new QwenTtsClient(_settings.Server.BaseUrl);
            var timeout = TimeSpan.FromSeconds(15);
            var stopwatch = Stopwatch.StartNew();

            // Retry for 15 seconds (server may still be loading)
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var isHealthy = await client.CheckHealthAsync();
                    if (isHealthy)
                    {
                        // Success - save settings and complete
                        _settings.Metadata.HasCompletedWizard = true;
                        _settings.Save();
                        ErrorMessage = "";
                        StatusMessage = "";
                        CurrentStep = WizardStep.Completed;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Health check attempt failed, will retry (elapsed: {Elapsed}s)", stopwatch.Elapsed.TotalSeconds);
                }

                // Wait 1 second before next retry
                await Task.Delay(1000);
            }

            // All retries failed after 15 seconds
            ErrorMessage = LocalizationManager.Get("WizardLocalServerNotFound") ?? "Could not connect to local server.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local server health check failed");
            ErrorMessage = LocalizationManager.Get("WizardLocalServerNotFound") ?? "Could not connect to local server.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Step 2.2 - Test connection to remote server.</summary>
    [RelayCommand]
    private async Task TestRemoteServerAsync()
    {
        IsLoading = true;
        StatusMessage = LocalizationManager.Get("WizardConnecting") ?? "Connecting…";
        ErrorMessage = "";

        try
        {
            // Validate URL format
            if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                ErrorMessage = LocalizationManager.Get("WizardInvalidUrl") ?? "Invalid URL format";
                return;
            }

            var client = new QwenTtsClient(ServerUrl);
            var isHealthy = await client.CheckHealthAsync();

            if (isHealthy)
            {
                _settings.Server.BaseUrl = ServerUrl;
                _settings.Metadata.HasCompletedWizard = true;
                _settings.Save();
                ErrorMessage = "";
                StatusMessage = "";
                CurrentStep = WizardStep.Completed;
            }
            else
            {
                ErrorMessage = LocalizationManager.Get("WizardRemoteServerNotReachable") ?? "Could not reach the remote server.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote server health check failed");
            ErrorMessage = LocalizationManager.Get("WizardRemoteServerNotReachable") ?? "Could not reach the remote server.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Step 3.0 - Install WSL (Windows only).</summary>
    [RelayCommand]
    private async Task InstallWslAsync()
    {
        IsLoading = true;
        StatusMessage = LocalizationManager.Get("WizardInstallingWsl") ?? "Installing WSL…";
        ErrorMessage = "";

        try
        {
            var progress = new Progress<string>(msg => StatusMessage = msg);
            var (success, output) = await _platformService.InstallWslAsync(progress);

            if (success)
            {
                StatusMessage = LocalizationManager.Get("WizardWslInstalled") ?? "WSL installed successfully.";
                // Auto-advance after short delay
                await Task.Delay(2000);
                ErrorMessage = "";
                StatusMessage = "";
                CurrentStep = WizardStep.DockerInstall;
            }
            else
            {
                ErrorMessage = $"{LocalizationManager.Get("WizardWslInstallFailed") ?? "WSL installation failed."}\n{output}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WSL installation failed");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Step 3.1 - Verify Docker is installed.</summary>
    [RelayCommand]
    private async Task VerifyDockerAsync()
    {
        _logger.LogInformation("VerifyDockerAsync called - starting Docker verification");
        
        IsLoading = true;
        StatusMessage = LocalizationManager.Get("WizardVerifyingDocker") ?? "Verifying Docker installation…";
        ErrorMessage = "";

        try
        {
            _logger.LogInformation("Calling IsDockerInstalledAsync");
            var isInstalled = await _platformService.IsDockerInstalledAsync();
            _logger.LogInformation("Docker installed check result: {IsInstalled}", isInstalled);

            if (isInstalled)
            {
                // Docker is installed - proceed to resource config
                _logger.LogInformation("Docker verification successful - proceeding to ResourceConfig step");
                ErrorMessage = "";
                StatusMessage = "";
                CurrentStep = WizardStep.ResourceConfig;
            }
            else
            {
                _logger.LogWarning("Docker verification failed - docker not found");
                ErrorMessage = LocalizationManager.Get("WizardDockerNotFound") ?? "Docker is not installed or not running.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Docker verification");
            ErrorMessage = $"Error checking Docker: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _logger.LogInformation("VerifyDockerAsync completed, IsLoading={IsLoading}", IsLoading);
        }
    }

    /// <summary>Step 3.1 - Open Docker Desktop download page.</summary>
    [RelayCommand]
    private void OpenDockerDownloadPage()
    {
        try
        {
            var url = _platformService.IsWindows
                ? "https://www.docker.com/products/docker-desktop/"
                : "https://docs.docker.com/desktop/install/linux/";

            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Docker download page");
            ErrorMessage = "Failed to open browser.";
        }
    }

    /// <summary>Step 4.2 - Launch Docker server and wait for health check.</summary>
    [RelayCommand]
    private async Task LaunchServerAsync()
    {
        IsLoading = true;
        StatusMessage = LocalizationManager.Get("WizardLaunchingServer") ?? "Setting up the server…";
        ErrorMessage = "";

        try
        {
            // 1. Update docker-compose.yml with user's resource choices
            var dockerComposePath = _platformService.GetDockerComposePath();
            _platformService.UpdateDockerComposeResources(dockerComposePath, MemoryGb, CpuPercent);

            // 2. Launch Docker Compose
            var progress = new Progress<string>(msg => StatusMessage = msg);
            var (success, output) = await _platformService.LaunchDockerComposeAsync(
                AppDomain.CurrentDomain.BaseDirectory,
                progress);

            if (!success)
            {
                ErrorMessage = $"{LocalizationManager.Get("WizardDockerLaunchFailed") ?? "Failed to launch Docker Compose."}\n{output}";
                return;
            }

            // 3. Poll health endpoint (max 5 minutes, check every 5 seconds)
            StatusMessage = LocalizationManager.Get("WizardWaitingForServer") ?? "Waiting for server to start…";
            var client = new QwenTtsClient("http://localhost:7860");
            var timeout = TimeSpan.FromMinutes(5);
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                var isHealthy = await client.CheckHealthAsync();
                if (isHealthy)
                {
                    _settings.Server.BaseUrl = "http://localhost:7860";
                    _settings.Metadata.HasCompletedWizard = true;
                    _settings.Save();
                    ErrorMessage = "";
                    StatusMessage = "";
                    CurrentStep = WizardStep.Completed;
                    return;
                }

                await Task.Delay(5000); // Wait 5 seconds between checks
            }

            // Timeout - show warning but allow user to keep waiting
            ErrorMessage = LocalizationManager.Get("WizardServerStartTimeout") ?? "Server startup is taking longer than expected.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Server launch failed");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Check if WSL needs to be installed (Windows only).</summary>
    [RelayCommand]
    private async Task CheckWslRequirementAsync()
    {
        if (!_platformService.IsWindows)
        {
            // Skip WSL check on non-Windows platforms
            ErrorMessage = "";
            StatusMessage = "";
            CurrentStep = WizardStep.DockerInstall;
            return;
        }

        IsLoading = true;
        ErrorMessage = "";
        StatusMessage = "";
        
        try
        {
            var isInstalled = await _platformService.IsWslInstalledAsync();
            if (isInstalled)
            {
                // WSL already installed - skip to Docker
                CurrentStep = WizardStep.DockerInstall;
            }
            else
            {
                // Need to install WSL
                CurrentStep = WizardStep.WslInstall;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
