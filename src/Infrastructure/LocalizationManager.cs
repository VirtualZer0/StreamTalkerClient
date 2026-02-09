using Avalonia;

namespace StreamTalkerClient.Infrastructure;

/// <summary>
/// Helper for retrieving localized strings from resource dictionaries.
/// </summary>
public static class LocalizationManager
{
    /// <summary>
    /// Get a localized string by key, returning null if not found.
    /// </summary>
    public static string? Get(string key)
    {
        if (Application.Current != null &&
            Application.Current.TryGetResource(key, Application.Current.ActualThemeVariant, out var resource) &&
            resource is string str)
        {
            return str;
        }

        return null;
    }

    /// <summary>
    /// Get a localized string by key, with a fallback value if not found.
    /// </summary>
    public static string Get(string key, string fallback)
    {
        return Get(key) ?? fallback;
    }
}
