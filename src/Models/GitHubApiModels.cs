using System.Text.Json.Serialization;

namespace StreamTalkerClient.Models;

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = [];
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";
}

public class UpdateInfo
{
    public required string CurrentVersion { get; init; }
    public required string NewVersion { get; init; }
    public required string Changelog { get; init; }
    public required string ReleaseUrl { get; init; }
    public required string AssetName { get; init; }
    public required long AssetSize { get; init; }
    public required string DownloadUrl { get; init; }
}

public class ServerInfoResponse
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";
}

public class ServerUpdateInfo
{
    public required string CurrentVersion { get; init; }
    public required string NewVersion { get; init; }
    public required string Changelog { get; init; }
    public required string ReleaseUrl { get; init; }
    public required bool IsLocalServer { get; init; }
    public required string DockerComposeDir { get; init; }
}
