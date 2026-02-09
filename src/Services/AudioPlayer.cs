using Microsoft.Extensions.Logging;
using NetCoreAudio;
using StreamTalkerClient.Infrastructure.Logging;

namespace StreamTalkerClient.Services;

public class AudioPlayer : IDisposable
{
    private readonly ILogger<AudioPlayer> _logger;
    private readonly Player _player;
    private readonly string _tempFolder;

    private byte _volume = 100;
    private string? _currentTempFile;
    private bool _disposed;

    public bool IsPlaying => _player.Playing;

    public float Volume
    {
        get => _volume / 100f;
        set
        {
            _volume = (byte)Math.Clamp(value * 100, 0, 100);
            _ = _player.SetVolume(_volume);
        }
    }

    public event EventHandler? PlaybackFinished;

    public AudioPlayer()
    {
        _logger = AppLoggerFactory.CreateLogger<AudioPlayer>();
        _player = new Player();
        _player.PlaybackFinished += OnPlaybackFinished;

        _tempFolder = Path.Combine(Path.GetTempPath(), "TwitchTTS");
        Directory.CreateDirectory(_tempFolder);
    }

    private void OnPlaybackFinished(object? sender, EventArgs e)
    {
        _logger.LogDebug("Playback finished");
        CleanupTempFile();
        PlaybackFinished?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> PlayAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Audio file not found: {FilePath}", filePath);
            return false;
        }

        try
        {
            CleanupTempFile();
            await _player.SetVolume(_volume).ConfigureAwait(false);
            await _player.Play(filePath).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play audio file: {FilePath}", filePath);
            return false;
        }
    }

    public bool Play(string filePath)
    {
        return PlayAsync(filePath).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task<bool> PlayAsync(byte[] audioData)
    {
        try
        {
            CleanupTempFile();

            // NetCoreAudio needs a file path, so save to temp file
            _currentTempFile = Path.Combine(_tempFolder, $"{Guid.NewGuid()}.wav");
            await File.WriteAllBytesAsync(_currentTempFile, audioData).ConfigureAwait(false);

            await _player.SetVolume(_volume).ConfigureAwait(false);
            await _player.Play(_currentTempFile).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play audio data");
            CleanupTempFile();
            return false;
        }
    }

    public bool Play(byte[] audioData)
    {
        return PlayAsync(audioData).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async Task StopAsync()
    {
        try
        {
            await _player.Stop().ConfigureAwait(false);
            CleanupTempFile();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping playback");
        }
    }

    public void Stop()
    {
        StopAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private void CleanupTempFile()
    {
        var tempFile = _currentTempFile;
        _currentTempFile = null;

        if (tempFile != null && File.Exists(tempFile))
        {
            try
            {
                File.Delete(tempFile);
            }
            catch (IOException)
            {
                // Expected - file might still be in use by audio player
                _logger.LogDebug("Could not delete temp file (in use): {FilePath}", tempFile);
            }
            catch (Exception ex)
            {
                // Unexpected error - log it
                _logger.LogWarning(ex, "Failed to delete temp file: {FilePath}", tempFile);
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _player.PlaybackFinished -= OnPlaybackFinished;

            try
            {
                Stop();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping playback during dispose");
            }

            // Dispose the NetCoreAudio player
            if (_player is IDisposable disposablePlayer)
            {
                disposablePlayer.Dispose();
            }

            // Cleanup temp folder
            try
            {
                if (Directory.Exists(_tempFolder))
                {
                    Directory.Delete(_tempFolder, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not delete temp folder: {Folder}", _tempFolder);
            }
        }

        _disposed = true;
    }
}
