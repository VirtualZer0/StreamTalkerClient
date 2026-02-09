using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace StreamTalkerClient.Services.TtsApi;

/// <summary>
/// Base class for TTS API clients. Provides shared HTTP infrastructure
/// including a common <see cref="HttpClient"/> and base URL management.
/// All sub-clients inherit from this to share a single HTTP connection pool.
/// </summary>
public abstract class TtsApiBase
{
    /// <summary>Shared HTTP client for all TTS API requests.</summary>
    protected readonly HttpClient Http;

    /// <summary>Logger instance for the derived client.</summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The base URL of the TTS server (no trailing slash).
    /// Updated via <see cref="SetBaseUrl"/> when the user changes the server address.
    /// Marked volatile for safe cross-thread reads.
    /// </summary>
    protected volatile string BaseUrl;

    /// <summary>
    /// Initializes a new TTS API sub-client with the shared HTTP client and base URL.
    /// </summary>
    /// <param name="http">Shared <see cref="HttpClient"/> instance (owned by the facade).</param>
    /// <param name="baseUrl">Normalized base URL of the TTS server.</param>
    /// <param name="logger">Logger for the derived client class.</param>
    protected TtsApiBase(HttpClient http, string baseUrl, ILogger logger)
    {
        Http = http ?? throw new ArgumentNullException(nameof(http));
        BaseUrl = baseUrl;
        Logger = logger;
    }

    /// <summary>
    /// Updates the base URL for this sub-client.
    /// Called by the <see cref="QwenTtsClient"/> facade when the server address changes.
    /// </summary>
    /// <param name="baseUrl">New normalized base URL (no trailing slash).</param>
    public void SetBaseUrl(string baseUrl)
    {
        BaseUrl = baseUrl;
    }

    /// <summary>
    /// Validates and normalizes a TTS server URL (trims trailing slashes, checks scheme).
    /// </summary>
    /// <param name="url">Raw URL to validate.</param>
    /// <returns>Normalized URL string.</returns>
    /// <exception cref="ArgumentException">If the URL is empty, malformed, or uses a non-HTTP scheme.</exception>
    public static string ValidateAndNormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty", nameof(url));

        var trimmed = url.TrimEnd('/');

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid URL format: {url}", nameof(url));

        if (uri.Scheme != "http" && uri.Scheme != "https")
            throw new ArgumentException($"URL must use http or https scheme: {url}", nameof(url));

        return trimmed;
    }
}
