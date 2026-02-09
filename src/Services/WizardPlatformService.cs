using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure;

namespace StreamTalkerClient.Services;

/// <summary>
/// Platform-specific operations for the first-launch wizard.
/// Handles WSL/Docker detection, installation, and process execution.
/// </summary>
public class WizardPlatformService
{
    private readonly ILogger<WizardPlatformService> _logger;
    private readonly string _dockerComposePath;

    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public WizardPlatformService(ILogger<WizardPlatformService> logger)
    {
        _logger = logger;

        // Path where docker-compose.yml will be extracted
        _dockerComposePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "docker-compose.yml"
        );

        // Extract embedded docker-compose.yml if it doesn't exist
        EnsureDockerComposeExists();
    }

    private void EnsureDockerComposeExists()
    {
        if (File.Exists(_dockerComposePath))
        {
            _logger.LogDebug("docker-compose.yml already exists at {Path}", _dockerComposePath);
            return;
        }

        try
        {
            var resourceName = EmbeddedResourceHelper.GetResourceName("docker-compose.yml");

            if (!EmbeddedResourceHelper.ResourceExists(resourceName))
            {
                _logger.LogError("Embedded docker-compose.yml resource not found!");
                throw new FileNotFoundException("docker-compose.yml not embedded in executable.");
            }

            _logger.LogInformation("Extracting docker-compose.yml to {Path}", _dockerComposePath);
            EmbeddedResourceHelper.ExtractResource(resourceName, _dockerComposePath);
            _logger.LogInformation("docker-compose.yml extracted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract docker-compose.yml");
            throw;
        }
    }

    /// <summary>Check if WSL is installed (Windows only).</summary>
    public async Task<bool> IsWslInstalledAsync()
    {
        if (!IsWindows) return true; // N/A on Linux

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = "--status",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WSL check failed");
            return false;
        }
    }

    /// <summary>Install WSL via 'wsl --install'.</summary>
    public async Task<(bool Success, string Output)> InstallWslAsync(IProgress<string> progress)
    {
        if (!IsWindows)
        {
            return (false, "WSL installation is only supported on Windows.");
        }

        try
        {
            progress.Report("Starting WSL installation (requesting admin elevation)...");

            // UseShellExecute=true is required for Verb="runas" to work (admin elevation).
            // This means we cannot redirect stdout/stderr, so we report progress generically.
            var psi = new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = "--install",
                UseShellExecute = true,
                Verb = "runas"
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (false, "Failed to start WSL installation process.");
            }

            progress.Report("WSL installation in progress (elevated process running)...");
            await process.WaitForExitAsync();

            var success = process.ExitCode == 0;
            var resultMessage = success
                ? "WSL installation completed successfully."
                : $"WSL installation exited with code {process.ExitCode}.";
            progress.Report(resultMessage);
            return (success, resultMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WSL installation failed");
            return (false, ex.Message);
        }
    }

    /// <summary>Check if Docker is installed and running.</summary>
    public async Task<bool> IsDockerInstalledAsync()
    {
        _logger.LogInformation("IsDockerInstalledAsync called - checking Docker availability");
        
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _logger.LogInformation("Starting docker --version process");
            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogWarning("Failed to start docker process - Process.Start returned null");
                return false;
            }

            await process.WaitForExitAsync();
            _logger.LogInformation("Docker process exited with code: {ExitCode}", process.ExitCode);
            
            if (process.ExitCode == 0)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                _logger.LogInformation("Docker version: {Output}", output.Trim());
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogWarning("Docker check failed: {Error}", error.Trim());
            }
            
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while checking Docker installation");
            return false;
        }
    }

    /// <summary>Launch Docker Compose in project directory.</summary>
    public async Task<(bool Success, string Output)> LaunchDockerComposeAsync(
        string workingDirectory,
        IProgress<string> progress)
    {
        try
        {
            progress.Report("Starting Docker Compose...");

            var psi = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "compose up -d",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (false, "Failed to start Docker Compose process.");
            }

            var output = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.AppendLine(e.Data);
                    progress.Report(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.AppendLine(e.Data);
                    progress.Report(e.Data);
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            var success = process.ExitCode == 0;
            return (success, output.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Docker Compose launch failed");
            return (false, ex.Message);
        }
    }

    /// <summary>Update docker-compose.yml resource limits.</summary>
    public void UpdateDockerComposeResources(string filePath, int memoryGb, int cpuPercent)
    {
        var content = File.ReadAllText(filePath);

        // Regex replace for memory limit (line 17)
        content = Regex.Replace(content,
            @"memory:\s*\d+G",
            $"memory: {memoryGb}G");

        // Regex replace for CPU limit (line 16)
        var cpuDecimal = (cpuPercent / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        content = Regex.Replace(content,
            @"cpus:\s*'[\d.]+' # limit",
            $"cpus: '{cpuDecimal}'",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));

        // Fallback if comment is missing
        if (!content.Contains($"cpus: '{cpuDecimal}'"))
        {
            content = Regex.Replace(content,
                @"cpus:\s*'[\d.]+'",
                $"cpus: '{cpuDecimal}'",
                RegexOptions.None,
                TimeSpan.FromSeconds(1));
        }

        File.WriteAllText(filePath, content);
        _logger.LogInformation("Updated docker-compose.yml: {MemoryGb}GB RAM, {CpuPercent}% CPU", memoryGb, cpuPercent);
    }

    /// <summary>Get the path to the extracted docker-compose.yml.</summary>
    public string GetDockerComposePath() => _dockerComposePath;
}
