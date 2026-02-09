namespace StreamTalkerClient.Models;

public record VoiceInfo
{
    public string Name { get; init; } = "";
    public string? Language { get; init; }
    public string? Description { get; init; }
    public List<string>? CachedModels { get; init; }

    public bool HasCached06B => CachedModels?.Contains("0.6B") == true;
    public bool HasCached17B => CachedModels?.Contains("1.7B") == true;

    public override string ToString() => Name;
}
