using System.Text.Json;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;

namespace StreamTalkerClient.Managers;

public class CacheManager : IDisposable
{
    private readonly ILogger<CacheManager> _logger;
    private readonly string _cacheDir;
    private readonly string _indexPath;
    private readonly object _lock = new();
    private readonly System.Timers.Timer _saveDebounceTimer;
    private readonly HashSet<string> _pinnedKeys = new();
    private readonly ManualResetEventSlim _flushComplete = new(true);

    private Dictionary<string, CacheEntry> _index;
    private long _cacheLimitBytes;
    private bool _indexDirty;
    private bool _disposed;
    private long _currentSizeBytes;

    public long CurrentSizeBytes => Interlocked.Read(ref _currentSizeBytes);
    public int ItemCount { get { lock (_lock) return _index.Count; } }
    public long CacheLimitBytes => Interlocked.Read(ref _cacheLimitBytes);

    public CacheManager(string cacheDir = "cache", int cacheLimitMB = 150)
    {
        _logger = AppLoggerFactory.CreateLogger<CacheManager>();
        _cacheDir = cacheDir;
        _indexPath = Path.Combine(_cacheDir, "index.json");
        _cacheLimitBytes = cacheLimitMB * 1024L * 1024L;
        _index = new Dictionary<string, CacheEntry>();

        // Debounced save timer (save at most every N milliseconds)
        _saveDebounceTimer = new System.Timers.Timer(Infrastructure.AppConstants.Cache.IndexSaveDebounceMs);
        _saveDebounceTimer.Elapsed += (_, _) => FlushIndexIfDirty();
        _saveDebounceTimer.AutoReset = false;

        EnsureCacheDirectory();
        LoadIndex();

        _logger.LogInformation("Cache initialized: {CacheDir}, Limit: {LimitMB}MB, Items: {Count}",
            _cacheDir, cacheLimitMB, _index.Count);
    }

    public void SetCacheLimit(int limitMB)
    {
        Interlocked.Exchange(ref _cacheLimitBytes, limitMB * 1024L * 1024L);
        EvictIfNeeded();
    }

    private void EnsureCacheDirectory()
    {
        if (!Directory.Exists(_cacheDir))
        {
            Directory.CreateDirectory(_cacheDir);
        }
    }

    private void LoadIndex()
    {
        lock (_lock)
        {
            if (File.Exists(_indexPath))
            {
                try
                {
                    var json = File.ReadAllText(_indexPath);
                    _index = JsonSerializer.Deserialize(json, Infrastructure.AppJsonSerializerContext.Default.DictionaryStringCacheEntry)
                             ?? new Dictionary<string, CacheEntry>();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load cache index, starting fresh");
                    _index = new Dictionary<string, CacheEntry>();
                }
            }

            // Validate entries and calculate size
            var keysToRemove = new List<string>();
            Interlocked.Exchange(ref _currentSizeBytes, 0);

            foreach (var kvp in _index)
            {
                var filePath = GetFilePath(kvp.Key);
                if (!File.Exists(filePath))
                {
                    keysToRemove.Add(kvp.Key);
                }
                else
                {
                    Interlocked.Add(ref _currentSizeBytes, kvp.Value.SizeBytes);
                }
            }

            foreach (var key in keysToRemove)
            {
                _index.Remove(key);
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogDebug("Removed {Count} orphaned cache entries", keysToRemove.Count);
                MarkDirtyAndScheduleSave();
            }
        }
    }

    private void MarkDirtyAndScheduleSave()
    {
        _indexDirty = true;
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    private void FlushIndexIfDirty()
    {
        if (!_indexDirty)
            return;

        // Signal that flush is starting
        _flushComplete.Reset();
        try
        {
            Dictionary<string, CacheEntry> indexCopy;
            lock (_lock)
            {
                if (!_indexDirty)
                    return;

                indexCopy = new Dictionary<string, CacheEntry>(_index);
                _indexDirty = false;
            }

            // Write outside lock
            try
            {
                var json = JsonSerializer.Serialize(indexCopy, Infrastructure.AppJsonSerializerContext.Default.DictionaryStringCacheEntry);
                File.WriteAllText(_indexPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save cache index");
            }
        }
        finally
        {
            _flushComplete.Set();
        }
    }

    private string GetFilePath(string cacheKey)
    {
        return Path.Combine(_cacheDir, $"{cacheKey}.wav");
    }

    /// <summary>
    /// Pin a cache key to prevent its eviction during playback
    /// </summary>
    public void Pin(string cacheKey)
    {
        lock (_lock)
        {
            _pinnedKeys.Add(cacheKey);
        }
    }

    /// <summary>
    /// Unpin a cache key, allowing it to be evicted
    /// </summary>
    public void Unpin(string cacheKey)
    {
        lock (_lock)
        {
            _pinnedKeys.Remove(cacheKey);
        }
    }

    public bool TryGet(string cacheKey, out string? filePath)
    {
        lock (_lock)
        {
            if (_index.TryGetValue(cacheKey, out var entry))
            {
                var path = GetFilePath(cacheKey);
                if (File.Exists(path))
                {
                    entry.LastAccessTime = DateTime.UtcNow;
                    entry.HitCount++;
                    MarkDirtyAndScheduleSave();
                    filePath = path;
                    return true;
                }

                // File doesn't exist, remove from index
                _index.Remove(cacheKey);
                MarkDirtyAndScheduleSave();
            }

            filePath = null;
            return false;
        }
    }

    public string Store(string cacheKey, byte[] audioData)
    {
        var filePath = GetFilePath(cacheKey);

        // Write file OUTSIDE lock
        try
        {
            File.WriteAllBytes(filePath, audioData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write cache file: {FilePath}", filePath);
            throw;
        }

        lock (_lock)
        {
            // Update index
            var entry = new CacheEntry
            {
                SizeBytes = audioData.Length,
                CreatedTime = DateTime.UtcNow,
                LastAccessTime = DateTime.UtcNow
            };

            if (_index.TryGetValue(cacheKey, out var existing))
            {
                Interlocked.Add(ref _currentSizeBytes, -existing.SizeBytes);
            }

            _index[cacheKey] = entry;
            Interlocked.Add(ref _currentSizeBytes, audioData.Length);

            MarkDirtyAndScheduleSave();
            EvictIfNeededInternal();
        }

        return filePath;
    }

    public bool Contains(string cacheKey)
    {
        lock (_lock)
        {
            return _index.ContainsKey(cacheKey) && File.Exists(GetFilePath(cacheKey));
        }
    }

    private void EvictIfNeeded()
    {
        lock (_lock)
        {
            EvictIfNeededInternal();
        }
    }

    private void EvictIfNeededInternal()
    {
        if (CurrentSizeBytes <= CacheLimitBytes)
            return;

        // Step 1: Compress - remove unused entries (HitCount == 0)
        var (compressedCount, compressedBytes) = RemoveUnusedEntriesInternal();
        if (compressedCount > 0)
        {
            _logger.LogInformation("Cache auto-compress: removed {Count} unused entries, freed {MB:F2} MB",
                compressedCount, compressedBytes / (1024.0 * 1024.0));
        }

        // Step 2: Check if we have at least 15% free space (below 85% threshold)
        var usagePercent = GetUsagePercent();
        if (usagePercent <= Infrastructure.AppConstants.Cache.EvictionThresholdPercent * 100)
        {
            _logger.LogDebug("Cache usage at {UsagePercent:F1}% after compress, below threshold, no further eviction needed", usagePercent);
            return;
        }

        _logger.LogInformation("Cache usage at {UsagePercent:F1}% after compress, above threshold, removing 50% of oldest entries", usagePercent);

        // Step 3: Remove 50% of cache entries sorted by LastAccessTime (oldest first)
        var candidates = _index
            .Where(x => !_pinnedKeys.Contains(x.Key))
            .OrderBy(x => x.Value.LastAccessTime)
            .ToList();

        var countToRemove = Math.Max(1, candidates.Count / 2);
        var evicted = 0;
        long freedBytes = 0;

        foreach (var kvp in candidates.Take(countToRemove))
        {
            var filePath = GetFilePath(kvp.Key);
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                freedBytes += kvp.Value.SizeBytes;
                Interlocked.Add(ref _currentSizeBytes, -kvp.Value.SizeBytes);
                _index.Remove(kvp.Key);
                evicted++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete cache file: {FilePath}", filePath);
            }
        }

        if (evicted > 0)
        {
            _logger.LogInformation("Evicted {Count} oldest cache entries, freed {MB:F2} MB",
                evicted, freedBytes / (1024.0 * 1024.0));
            MarkDirtyAndScheduleSave();
        }

        // Step 4: Clean up orphaned files (files not in index)
        CleanupOrphanedFilesInternal();
    }

    private (int count, long bytes) RemoveUnusedEntriesInternal()
    {
        var toRemove = _index
            .Where(x => x.Value.HitCount == 0 && !_pinnedKeys.Contains(x.Key))
            .ToList();

        long freedBytes = 0;
        int removedCount = 0;

        foreach (var kvp in toRemove)
        {
            var filePath = GetFilePath(kvp.Key);
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);

                freedBytes += kvp.Value.SizeBytes;
                Interlocked.Add(ref _currentSizeBytes, -kvp.Value.SizeBytes);
                _index.Remove(kvp.Key);
                removedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete cache file: {FilePath}", filePath);
            }
        }

        if (removedCount > 0)
        {
            MarkDirtyAndScheduleSave();
        }

        return (removedCount, freedBytes);
    }

    private void CleanupOrphanedFilesInternal()
    {
        try
        {
            var indexedKeys = new HashSet<string>(_index.Keys);
            var orphanedCount = 0;
            long orphanedBytes = 0;

            foreach (var filePath in Directory.GetFiles(_cacheDir, "*.wav"))
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (!indexedKeys.Contains(fileName))
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        orphanedBytes += fileInfo.Length;
                        File.Delete(filePath);
                        orphanedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete orphaned file: {FilePath}", filePath);
                    }
                }
            }

            if (orphanedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} orphaned files, freed {MB:F2} MB",
                    orphanedCount, orphanedBytes / (1024.0 * 1024.0));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup orphaned files in: {CacheDir}", _cacheDir);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            // Delete all WAV files in cache directory, not just indexed ones
            try
            {
                foreach (var filePath in Directory.GetFiles(_cacheDir, "*.wav"))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cache file: {FilePath}", filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enumerate cache directory: {CacheDir}", _cacheDir);
            }

            _index.Clear();
            _pinnedKeys.Clear();
            Interlocked.Exchange(ref _currentSizeBytes, 0);
            MarkDirtyAndScheduleSave();

            _logger.LogInformation("Cache cleared");
        }
    }

    public double GetUsagePercent()
    {
        var limit = CacheLimitBytes;
        if (limit == 0) return 0;
        return (double)CurrentSizeBytes / limit * 100;
    }

    /// <summary>
    /// Gets statistics about cache entries that were never accessed.
    /// </summary>
    public (int count, long sizeBytes) GetUnusedEntriesStats()
    {
        lock (_lock)
        {
            var entries = _index
                .Where(x => x.Value.HitCount == 0 && !_pinnedKeys.Contains(x.Key))
                .ToList();
            return (entries.Count, entries.Sum(x => x.Value.SizeBytes));
        }
    }

    /// <summary>
    /// Removes cache entries that were never accessed after creation.
    /// Pinned entries are not removed.
    /// </summary>
    public (int removedCount, long freedBytes) RemoveUnusedEntries()
    {
        lock (_lock)
        {
            var (count, bytes) = RemoveUnusedEntriesInternal();
            if (count > 0)
            {
                _logger.LogInformation("Cache compressed: removed {Count} entries, freed {MB:F2} MB",
                    count, bytes / (1024.0 * 1024.0));
            }
            return (count, bytes);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Dispose();

        // Wait for any in-progress flush to complete
        _flushComplete.Wait(TimeSpan.FromSeconds(2));

        // Final flush
        FlushIndexIfDirty();

        _flushComplete.Dispose();
    }
}

public class CacheEntry
{
    public long SizeBytes { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime LastAccessTime { get; set; }
    public int HitCount { get; set; }
}
