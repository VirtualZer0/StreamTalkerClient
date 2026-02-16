namespace StreamTalkerClient.Models;

/// <summary>
/// Represents a blacklisted user who should be ignored from TTS entirely.
/// </summary>
public class BlacklistEntry
{
    public string Username { get; set; } = "";
    public string Platform { get; set; } = "Any"; // "Any", "Twitch", "VKPlay"
    public bool IsEnabled { get; set; } = true;
}
