namespace StreamTalkerClient.Models;

/// <summary>
/// Unified connection states for all streaming services.
/// </summary>
public enum StreamConnectionState
{
    /// <summary>Not connected to the streaming service.</summary>
    Disconnected = 0,

    /// <summary>Connection in progress (initializing, getting tokens, etc.).</summary>
    Connecting,

    /// <summary>Connected to the service but not yet in a channel.</summary>
    Connected,

    /// <summary>Successfully joined/subscribed to the channel.</summary>
    Joined,

    /// <summary>Connection error occurred.</summary>
    Error
}
