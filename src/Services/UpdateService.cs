using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Services;

public class UpdateService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService> _logger;
    private CancellationTokenSource? _downloadCts;

    public UpdateService()
    {
        _logger = AppLoggerFactory.CreateLogger<UpdateService>();
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "StreamTalkerClient");
        _httpClient.Timeout = TimeSpan.FromSeconds(AppConstants.Update.CheckTimeoutSeconds);
    }

    public static string GetCurrentVersion()
    {
        var attr = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var version = attr?.InformationalVersion ?? "0.0.0-dev";

        // Strip +commitHash suffix if present
        var plusIdx = version.IndexOf('+');
        if (plusIdx >= 0)
            version = version[..plusIdx];

        return version;
    }

    public static bool IsDevBuild()
    {
        var version = GetCurrentVersion();
        return version.StartsWith("0.0.0") || version.Contains("-dev");
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"{AppConstants.Update.GitHubApiBase}/repos/{AppConstants.Update.GitHubRepoOwner}/{AppConstants.Update.GitHubRepoName}/releases/latest";

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub API returned {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var release = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.GitHubRelease);

            if (release == null)
                return null;

            var currentVersion = GetCurrentVersion();
            var newVersion = release.TagName.TrimStart('v');

            if (!IsNewerVersion(currentVersion, newVersion))
            {
                _logger.LogInformation("Current version {Current} is up to date (latest: {Latest})",
                    currentVersion, newVersion);
                return null;
            }

            // Find matching asset
            var asset = FindMatchingAsset(release.Assets, newVersion);
            if (asset == null)
            {
                _logger.LogWarning("No matching asset found for platform in release {Version}", newVersion);
                return null;
            }

            return new UpdateInfo
            {
                CurrentVersion = currentVersion,
                NewVersion = newVersion,
                Changelog = release.Body,
                ReleaseUrl = release.HtmlUrl,
                AssetName = asset.Name,
                AssetSize = asset.Size,
                DownloadUrl = asset.BrowserDownloadUrl
            };
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            return null;
        }
    }

    public async Task<string?> GetServerVersionAsync(string serverBaseUrl, CancellationToken ct = default)
    {
        try
        {
            var url = serverBaseUrl.TrimEnd('/') + "/";
            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var serverInfo = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.ServerInfoResponse);
            return serverInfo?.Version;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get server version from {Url}", serverBaseUrl);
            return null;
        }
    }

    public async Task<ServerUpdateInfo?> CheckForServerUpdateAsync(string currentServerVersion, string serverBaseUrl, CancellationToken ct = default)
    {
        try
        {
            var url = $"{AppConstants.Update.GitHubApiBase}/repos/{AppConstants.Update.GitHubRepoOwner}/{AppConstants.Update.ServerGitHubRepoName}/releases/latest";

            var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub API returned {StatusCode} for server release check", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var release = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.GitHubRelease);

            if (release == null)
                return null;

            var newVersion = release.TagName.TrimStart('v');

            if (!IsNewerVersion(currentServerVersion, newVersion))
            {
                _logger.LogInformation("Server version {Current} is up to date (latest: {Latest})",
                    currentServerVersion, newVersion);
                return null;
            }

            var isLocal = IsLocalServer(serverBaseUrl);
            var dockerComposeDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);

            return new ServerUpdateInfo
            {
                CurrentVersion = currentServerVersion,
                NewVersion = newVersion,
                Changelog = release.Body,
                ReleaseUrl = release.HtmlUrl,
                IsLocalServer = isLocal && File.Exists(Path.Combine(dockerComposeDir, "docker-compose.yml")),
                DockerComposeDir = dockerComposeDir
            };
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for server updates");
            return null;
        }
    }

    private static bool IsLocalServer(string serverUrl)
    {
        if (Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.ToLowerInvariant();
            return host is "localhost" or "127.0.0.1" or "::1";
        }
        return false;
    }

    public async Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _downloadCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _downloadCts.Token;

        string? targetPath = null;
        try
        {
            using var downloadClient = new HttpClient();
            downloadClient.Timeout = TimeSpan.FromMinutes(AppConstants.Update.DownloadTimeoutMinutes);

            var updatesDir = Path.Combine(AppSettings.DataFolder, AppConstants.Update.UpdateFolderName);
            Directory.CreateDirectory(updatesDir);
            targetPath = Path.Combine(updatesDir, updateInfo.AssetName);

            using var response = await downloadClient.GetAsync(updateInfo.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? updateInfo.AssetSize;
            await using var contentStream = await response.Content.ReadAsStreamAsync(token);
            await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write,
                FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long bytesRead = 0;

            while (true)
            {
                var read = await contentStream.ReadAsync(buffer, token);
                if (read == 0) break;

                await fileStream.WriteAsync(buffer.AsMemory(0, read), token);
                bytesRead += read;

                if (totalBytes > 0)
                    progress?.Report((double)bytesRead / totalBytes);
            }

            progress?.Report(1.0);
            _logger.LogInformation("Downloaded update to {Path} ({Bytes} bytes)", targetPath, bytesRead);
            return targetPath;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update download was cancelled");
            TryDeleteFile(targetPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download update");
            TryDeleteFile(targetPath);
            return null;
        }
    }

    public void CancelDownload()
    {
        _downloadCts?.Cancel();
    }

    public void ApplyUpdateAndRestart(string downloadedPath)
    {
        var appPath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Cannot determine current executable path");

        var appDir = Path.GetDirectoryName(appPath)!;
        var appFileName = Path.GetFileName(appPath);
        var pid = Environment.ProcessId;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ApplyUpdateWindows(downloadedPath, appDir, appFileName, pid);
        }
        else
        {
            ApplyUpdateLinux(downloadedPath, appDir, appFileName, pid);
        }

        // Shutdown the application
        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(0);
        }
        else
        {
            Environment.Exit(0);
        }
    }

    private void ApplyUpdateWindows(string downloadedPath, string appDir, string appFileName, int pid)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"stc_update_{pid}.bat");
        var ext = Path.GetExtension(downloadedPath).ToLowerInvariant();

        var script = $"""
            @echo off
            :wait
            tasklist /fi "PID eq {pid}" | findstr {pid} >nul && (timeout /t 1 >nul & goto wait)
            if /i "{ext}"==".zip" (
                powershell -NoProfile -Command "Expand-Archive -Path '{downloadedPath}' -DestinationPath '{appDir}' -Force"
            ) else (
                copy /y "{downloadedPath}" "{Path.Combine(appDir, appFileName)}"
            )
            start "" "{Path.Combine(appDir, appFileName)}"
            del /q "{downloadedPath}"
            del /q "{scriptPath}"
            """;

        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        _logger.LogInformation("Update script launched: {Script}", scriptPath);
    }

    private void ApplyUpdateLinux(string downloadedPath, string appDir, string appFileName, int pid)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"stc_update_{pid}.sh");

        var script = $"""
            #!/bin/bash
            while kill -0 {pid} 2>/dev/null; do sleep 1; done
            if [[ "{downloadedPath}" == *.tar.gz ]]; then
                tar -xzf "{downloadedPath}" -C "{appDir}"
            else
                cp "{downloadedPath}" "{Path.Combine(appDir, appFileName)}"
                chmod +x "{Path.Combine(appDir, appFileName)}"
            fi
            nohup "{Path.Combine(appDir, appFileName)}" >/dev/null 2>&1 &
            rm -f "{downloadedPath}" "{scriptPath}"
            """;

        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = scriptPath,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        _logger.LogInformation("Update script launched: {Script}", scriptPath);
    }

    private static void TryDeleteFile(string? path)
    {
        if (path != null && File.Exists(path))
        {
            try { File.Delete(path); }
            catch { /* best effort cleanup */ }
        }
    }

    private static bool IsNewerVersion(string current, string latest)
    {
        var currentClean = current.Split('-')[0];
        var latestClean = latest.Split('-')[0];
        var currentIsPreRelease = current.Contains('-');

        if (Version.TryParse(currentClean, out var currentVer) &&
            Version.TryParse(latestClean, out var latestVer))
        {
            // Strictly newer version
            if (latestVer > currentVer)
                return true;

            // Same base version but current is pre-release and latest is stable
            if (latestVer == currentVer && currentIsPreRelease && !latest.Contains('-'))
                return true;
        }

        return false;
    }

    private static GitHubAsset? FindMatchingAsset(List<GitHubAsset> assets, string version)
    {
        var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";

#if SELF_CONTAINED_BUILD
        // Self-contained: match asset with platform but without "-native"
        var prefix = $"{AppConstants.Update.AssetPrefix}{version}-{platform}";
        return assets.FirstOrDefault(a =>
            a.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            !a.Name.Contains("-native", StringComparison.OrdinalIgnoreCase));
#else
        // Framework-dependent (native): match asset with "-native"
        var nativePattern = $"{AppConstants.Update.AssetPrefix}{version}-{platform}-native";
        var nativeAsset = assets.FirstOrDefault(a =>
            a.Name.StartsWith(nativePattern, StringComparison.OrdinalIgnoreCase));

        if (nativeAsset != null)
            return nativeAsset;

        // Fallback: try old "-sc" naming (backward compat with older releases)
        // For non-self-contained, look for no-suffix asset
        var prefix = $"{AppConstants.Update.AssetPrefix}{version}-{platform}";
        return assets.FirstOrDefault(a =>
            a.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            !a.Name.Contains("-sc", StringComparison.OrdinalIgnoreCase) &&
            !a.Name.Contains("-native", StringComparison.OrdinalIgnoreCase));
#endif
    }

    public void Dispose()
    {
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }
}
