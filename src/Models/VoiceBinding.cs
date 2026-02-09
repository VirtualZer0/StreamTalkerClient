namespace StreamTalkerClient.Models;

/// <summary>
/// Represents a binding between a Twitch username and a TTS voice.
/// Messages from bound users bypass ReadAllMessages and RequireVoice settings.
/// </summary>
public class VoiceBinding
{
    public string Username { get; set; } = "";
    public string VoiceName { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
}
