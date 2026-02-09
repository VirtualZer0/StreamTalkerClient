using Microsoft.Extensions.Logging;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Services;

/// <summary>
/// Base class for streaming chat services with common functionality.
/// </summary>
public abstract class StreamingServiceBase : IStreamingService
{
    protected readonly ILogger _logger;
    protected bool _disposed;

    /// <summary>The current channel name, if connected.</summary>
    public string? Channel { get; protected set; }

    /// <summary>Current connection state.</summary>
    public StreamConnectionState ConnectionState { get; protected set; } = StreamConnectionState.Disconnected;

    /// <summary>Available rewards for the channel.</summary>
    public abstract IReadOnlyList<IStreamReward> Rewards { get; }

    /// <summary>Raised when connection state changes.</summary>
    public event EventHandler<StreamConnectionState>? OnConnectionStateChanged;

    /// <summary>Raised when a regular chat message is received.</summary>
    public event EventHandler<IStreamMessage>? OnMessage;

    /// <summary>Raised when a reward/channel points message is received.</summary>
    public event EventHandler<IStreamMessage>? OnReward;

    protected StreamingServiceBase(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sets the connection state and raises the OnConnectionStateChanged event.
    /// </summary>
    protected void SetConnectionState(StreamConnectionState state)
    {
        if (ConnectionState == state)
            return;

        ConnectionState = state;
        OnConnectionStateChanged?.Invoke(this, state);
    }

    /// <summary>
    /// Raises the OnMessage event with the specified message.
    /// </summary>
    protected void RaiseMessage(IStreamMessage message)
    {
        OnMessage?.Invoke(this, message);
    }

    /// <summary>
    /// Raises the OnReward event with the specified message.
    /// </summary>
    protected void RaiseReward(IStreamMessage message)
    {
        OnReward?.Invoke(this, message);
    }

    /// <summary>
    /// Connects to and joins the specified channel.
    /// </summary>
    public abstract Task JoinChannelAsync(string channel);

    /// <summary>
    /// Loads available rewards for the specified channel.
    /// </summary>
    public abstract Task LoadRewardsAsync(string channel);

    /// <summary>
    /// Disconnects from the current channel asynchronously.
    /// </summary>
    public abstract Task DisconnectAsync();

    /// <summary>
    /// Disconnects from the current channel (fire and forget).
    /// Uses a background task to avoid blocking the caller.
    /// </summary>
    public void Disconnect()
    {
        Task.Run(async () =>
        {
            try
            {
                await DisconnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in async disconnect");
            }
        });
    }

    /// <summary>
    /// Clears all event handlers. Called during disposal.
    /// </summary>
    protected void ClearEventHandlers()
    {
        OnConnectionStateChanged = null;
        OnMessage = null;
        OnReward = null;
    }

    /// <summary>
    /// Override in derived classes to perform cleanup.
    /// </summary>
    protected virtual void DisposeCore()
    {
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DisposeCore();
        ClearEventHandlers();
        GC.SuppressFinalize(this);
    }
}
