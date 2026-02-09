using StreamTalkerClient.Models;

namespace StreamTalkerClient.Services;

/// <summary>
/// Common interface for streaming chat services (Twitch, VK Play, etc.).
/// </summary>
public interface IStreamingService : IDisposable
{
    /// <summary>The current channel name, if connected.</summary>
    string? Channel { get; }

    /// <summary>Current connection state.</summary>
    StreamConnectionState ConnectionState { get; }

    /// <summary>Available rewards for the channel.</summary>
    IReadOnlyList<IStreamReward> Rewards { get; }

    /// <summary>Raised when connection state changes.</summary>
    event EventHandler<StreamConnectionState>? OnConnectionStateChanged;

    /// <summary>Raised when a regular chat message is received.</summary>
    event EventHandler<IStreamMessage>? OnMessage;

    /// <summary>Raised when a reward/channel points message is received.</summary>
    event EventHandler<IStreamMessage>? OnReward;

    /// <summary>
    /// Connects to and joins the specified channel.
    /// </summary>
    /// <param name="channel">The channel name to join.</param>
    Task JoinChannelAsync(string channel);

    /// <summary>
    /// Loads available rewards for the specified channel.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    Task LoadRewardsAsync(string channel);

    /// <summary>
    /// Disconnects from the current channel asynchronously.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Disconnects from the current channel (fire and forget).
    /// </summary>
    void Disconnect();
}
