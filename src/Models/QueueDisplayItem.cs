namespace StreamTalkerClient.Models;

public class QueueDisplayItem
{
    public string StateEmoji { get; set; } = "";
    public string StateName { get; set; } = "";
    public string VoiceName { get; set; } = "";
    public string Username { get; set; } = "";
    public string TextContent { get; set; } = "";
    public MessageState State { get; set; }
    public bool IsOverflowIndicator { get; set; }
}
