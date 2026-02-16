using System.Text.Json;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Infrastructure;

/// <summary>
/// Handles persistence (load/save), validation, and migration for <see cref="AppSettings"/>.
/// Separates file I/O concerns from the settings data model, improving testability and SRP.
/// </summary>
public static class SettingsRepository
{
    private static readonly string SettingsPath = Path.Combine(AppSettings.DataFolder, "settings.json");

    // ═══════════════════════════════════════════════════════════
    //  VALIDATION CONSTANTS
    // ═══════════════════════════════════════════════════════════

    private static readonly HashSet<string> ValidAttentionValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "auto", "sage_attn", "flex_attn", "flash2_attn", "sdpa", "eager"
    };

    private static readonly HashSet<string> ValidQuantizationValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "none", "int8", "float8"
    };

    private static readonly HashSet<string> ValidWarmupValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "none", "single", "batch"
    };

    private static readonly HashSet<string> ValidLanguageValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "Auto", "Chinese", "English", "Japanese", "Korean", "French", "German",
        "Spanish", "Portuguese", "Russian", "Arabic", "Italian"
    };

    private static readonly HashSet<string> ValidPlatformValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "Any", "Twitch", "VKPlay"
    };

    // ═══════════════════════════════════════════════════════════
    //  LOAD / SAVE
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Loads settings from <c>data/settings.json</c>. If the file doesn't exist or is corrupt,
    /// returns a fresh <see cref="AppSettings"/> with defaults. Automatically runs migration
    /// and validation.
    /// </summary>
    public static AppSettings Load()
    {
        ILogger? logger = null;
        try
        {
            logger = AppLoggerFactory.CreateLogger<AppSettings>();
        }
        catch
        {
            // Logger not initialized yet - OK during startup
        }

        if (File.Exists(SettingsPath))
        {
            try
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.AppSettings) ?? new AppSettings();
                var migrated = Migrate(settings);
                Validate(settings);
                if (migrated)
                {
                    logger?.LogInformation("Settings migrated to version {Version}", settings.Metadata.SettingsVersion);
                    Save(settings);
                }
                logger?.LogInformation("Settings loaded from {Path} (Volume={Volume}%)", SettingsPath, settings.Audio.VolumePercent);
                return settings;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to load settings from {Path}, using defaults", SettingsPath);
                return new AppSettings();
            }
        }

        logger?.LogInformation("No settings file found, using defaults");
        return new AppSettings();
    }

    /// <summary>
    /// Saves the given settings to <c>data/settings.json</c> as indented JSON.
    /// </summary>
    /// <param name="settings">The settings instance to persist.</param>
    public static void Save(AppSettings settings)
    {
        ILogger? logger = null;
        try
        {
            logger = AppLoggerFactory.CreateLogger<AppSettings>();
        }
        catch
        {
            // Logger not initialized - rare case
        }

        try
        {
            var json = JsonSerializer.Serialize(settings, AppJsonSerializerContext.Default.AppSettings);
            File.WriteAllText(SettingsPath, json);
            logger?.LogInformation("Settings saved to {Path} (Volume={Volume}%)", SettingsPath, settings.Audio.VolumePercent);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to save settings to {Path}", SettingsPath);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  MIGRATION
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Migrates settings from older versions to the current schema.
    /// Returns true if any migration was applied.
    /// </summary>
    private static bool Migrate(AppSettings settings)
    {
        bool migrated = false;

        if (settings.Metadata.SettingsVersion < 1)
        {
            // TTS API v2: attention/quantization enum changes
            if (settings.Model.Core.Attention == "flash_attention_2") settings.Model.Core.Attention = "flash2_attn";
            if (settings.Model.Core.Quantization == "int4") settings.Model.Core.Quantization = "float8";
            settings.Metadata.SettingsVersion = 1;
            migrated = true;
        }

        return migrated;
    }

    // ═══════════════════════════════════════════════════════════
    //  VALIDATION
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Validates and clamps all settings values to their allowed ranges.
    /// Replaces invalid enum values with defaults.
    /// </summary>
    private static void Validate(AppSettings s)
    {
        // Clamp numeric values to reasonable ranges
        s.Cache.LimitMB = Math.Clamp(s.Cache.LimitMB, 10, 10000);
        s.Audio.PlaybackDelaySeconds = Math.Clamp(s.Audio.PlaybackDelaySeconds, 0, 300);
        s.Inference.MaxBatchSize = Math.Clamp(s.Inference.MaxBatchSize, 1, 6);
        s.Model.AutoUnload.Minutes = Math.Clamp(s.Model.AutoUnload.Minutes, 1, 1440);
        s.Audio.VolumePercent = Math.Clamp(s.Audio.VolumePercent, 0, 100);
        s.Model.Warmup.TimeoutSeconds = Math.Clamp(s.Model.Warmup.TimeoutSeconds, 10, 600);
        s.Voice.Speed = Math.Clamp(s.Voice.Speed, 0.5, 2.0);
        s.Voice.Temperature = Math.Clamp(s.Voice.Temperature, 0.1, 2.0);
        s.Voice.MaxNewTokens = Math.Clamp(s.Voice.MaxNewTokens, 256, 8192);
        s.Voice.RepetitionPenalty = Math.Clamp(s.Voice.RepetitionPenalty, 1.0, 2.0);

        // Validate URL format
        if (string.IsNullOrWhiteSpace(s.Server.BaseUrl) ||
            !Uri.TryCreate(s.Server.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            s.Server.BaseUrl = "http://localhost:7860";
        }

        // Validate enum-like string values
        if (string.IsNullOrWhiteSpace(s.Model.Core.Name)) s.Model.Core.Name = "1.7B";
        if (string.IsNullOrWhiteSpace(s.Model.Core.Attention) || !ValidAttentionValues.Contains(s.Model.Core.Attention)) s.Model.Core.Attention = "auto";
        if (string.IsNullOrWhiteSpace(s.Model.Core.Quantization) || !ValidQuantizationValues.Contains(s.Model.Core.Quantization)) s.Model.Core.Quantization = "none";
        if (string.IsNullOrWhiteSpace(s.Model.Warmup.Mode) || !ValidWarmupValues.Contains(s.Model.Warmup.Mode)) s.Model.Warmup.Mode = "none";
        if (string.IsNullOrWhiteSpace(s.Server.Language) || !ValidLanguageValues.Contains(s.Server.Language)) s.Server.Language = "Russian";
        if (string.IsNullOrWhiteSpace(s.Voice.VoiceExtractionMode)) s.Voice.VoiceExtractionMode = "bracket";
        if (string.IsNullOrWhiteSpace(s.Hotkeys.SkipCurrentKey)) s.Hotkeys.SkipCurrentKey = "VcNumPad5";
        if (string.IsNullOrWhiteSpace(s.Hotkeys.SkipAllKey)) s.Hotkeys.SkipAllKey = "VcNumPad4";

        // Clean up per-voice volumes: remove empty keys and clamp values
        var invalidVolumeKeys = s.Audio.VoiceVolumes
            .Where(kv => string.IsNullOrWhiteSpace(kv.Key))
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in invalidVolumeKeys)
            s.Audio.VoiceVolumes.Remove(key);
        foreach (var key in s.Audio.VoiceVolumes.Keys.ToList())
            s.Audio.VoiceVolumes[key] = Math.Clamp(s.Audio.VoiceVolumes[key], 0, 100);

        // Remove voice bindings with missing data and validate platform values
        s.Voice.VoiceBindings.RemoveAll(b => string.IsNullOrWhiteSpace(b.Username) || string.IsNullOrWhiteSpace(b.VoiceName));
        foreach (var binding in s.Voice.VoiceBindings)
        {
            if (!ValidPlatformValues.Contains(binding.Platform))
                binding.Platform = "Any";
        }

        // Remove blacklist entries with missing data and validate platform values
        s.Voice.Blacklist.RemoveAll(b => string.IsNullOrWhiteSpace(b.Username));
        foreach (var entry in s.Voice.Blacklist)
        {
            if (!ValidPlatformValues.Contains(entry.Platform))
                entry.Platform = "Any";
        }
    }
}
