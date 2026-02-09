using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StreamTalkerClient.Infrastructure.Logging;
using StreamTalkerClient.Models;

namespace StreamTalkerClient.Services;

/// <summary>
/// Custom WebSocket client for VK Play Live Centrifugo server.
/// Uses JSON protocol (cf_protocol_version=v2) with proper Origin header.
/// </summary>
public class VKPlayWebSocket : IDisposable
{
    private const string WebSocketUrl = "wss://pubsub.live.vkvideo.ru/connection/websocket?cf_protocol_version=v2";
    private const string Origin = "https://live.vkvideo.ru";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<VKPlayWebSocket> _logger;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private int _messageId = 0;
    private string? _subscribedChannel;
    private bool _disposed;

    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;
    public event EventHandler<string>? OnError;
    public event EventHandler<VKChatMessage>? OnMessage;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public VKPlayWebSocket()
    {
        _logger = AppLoggerFactory.CreateLogger<VKPlayWebSocket>();
    }

    public async Task ConnectAsync(string chatChannel, string? token = null)
    {
        try
        {
            _subscribedChannel = chatChannel;
            _cts = new CancellationTokenSource();

            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("Origin", Origin);

            _logger.LogInformation("Connecting to VK Play WebSocket...");
            await _webSocket.ConnectAsync(new Uri(WebSocketUrl), _cts.Token);

            if (_webSocket.State != WebSocketState.Open)
            {
                _logger.LogError("WebSocket failed to open, state: {State}", _webSocket.State);
                OnError?.Invoke(this, "WebSocket failed to open");
                return;
            }

            _logger.LogInformation("WebSocket connected, sending connect message...");

            // Send connect message with token (anonymous token from /ws/connect API)
            var connectMsg = new VKWsRequest
            {
                Id = ++_messageId,
                Connect = new VKWsConnect
                {
                    Token = token ?? "",
                    Name = "js"
                }
            };

            await SendJsonAsync(connectMsg);

            // Start receiving messages
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to VK Play WebSocket");
            OnError?.Invoke(this, ex.Message);
        }
    }

    private async Task SendJsonAsync(object message)
    {
        if (_webSocket?.State != WebSocketState.Open)
            return;

        var json = JsonSerializer.Serialize(message, JsonOptions);
        _logger.LogDebug("Sending: {Json}", json);

        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        var messageBuffer = new StringBuilder();

        try
        {
            while (!ct.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("WebSocket closed by server");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        var json = messageBuffer.ToString();
                        messageBuffer.Clear();

                        await ProcessMessageAsync(json);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Receive loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in receive loop");
            OnError?.Invoke(this, ex.Message);
        }
        finally
        {
            OnDisconnected?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task ProcessMessageAsync(string json)
    {
        try
        {
            _logger.LogDebug("Received: {Json}", json);

            // Handle empty ping
            if (json == "{}")
            {
                await SendJsonAsync(new { });
                return;
            }

            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check for connect response
            if (root.TryGetProperty("id", out var idProp) && root.TryGetProperty("connect", out _))
            {
                _logger.LogInformation("Connected to Centrifugo, subscribing to channel: {Channel}", _subscribedChannel);
                OnConnected?.Invoke(this, EventArgs.Empty);

                // Subscribe to chat channel
                var subscribeMsg = new VKWsRequest
                {
                    Id = ++_messageId,
                    Subscribe = new VKWsSubscribe
                    {
                        Channel = _subscribedChannel
                    }
                };

                await SendJsonAsync(subscribeMsg);
                return;
            }

            // Check for subscribe response
            if (root.TryGetProperty("id", out _) && root.TryGetProperty("subscribe", out _))
            {
                _logger.LogInformation("Subscribed to chat channel successfully");
                return;
            }

            // Check for push message (chat message)
            // Structure: {"push":{"pub":{"data":{"type":"message","data":{...actual message...}}}}}
            if (root.TryGetProperty("push", out var push))
            {
                if (push.TryGetProperty("pub", out var pub))
                {
                    if (pub.TryGetProperty("data", out var outerData))
                    {
                        // Check if this is a message type
                        if (outerData.TryGetProperty("type", out var msgType) &&
                            msgType.GetString() == "message" &&
                            outerData.TryGetProperty("data", out var innerData))
                        {
                            var chatMessage = JsonSerializer.Deserialize(innerData.GetRawText(), Infrastructure.AppJsonSerializerContext.Default.VKChatMessage);
                            if (chatMessage?.Author != null)
                            {
                                _logger.LogDebug("Chat message from {Author}, IsReward: {IsReward}",
                                    chatMessage.Author.DisplayName, chatMessage.IsReward);
                                OnMessage?.Invoke(this, chatMessage);
                            }
                        }
                    }
                }
                return;
            }

            // Check for error
            if (root.TryGetProperty("error", out var error))
            {
                var errorMsg = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                _logger.LogError("Centrifugo error: {Error}", errorMsg);
                OnError?.Invoke(this, errorMsg ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message: {Json}", json);
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            _cts?.Cancel();

            if (_webSocket?.State == WebSocketState.Open)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }

            if (_receiveTask != null)
            {
                await Task.WhenAny(_receiveTask, Task.Delay(1000));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect");
        }
        finally
        {
            _webSocket?.Dispose();
            _webSocket = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _webSocket?.Dispose();
        _cts?.Dispose();
    }
}

// Request DTOs for VK Play Centrifugo JSON protocol
internal class VKWsRequest
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("connect")]
    public VKWsConnect? Connect { get; set; }

    [JsonPropertyName("subscribe")]
    public VKWsSubscribe? Subscribe { get; set; }
}

internal class VKWsConnect
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal class VKWsSubscribe
{
    [JsonPropertyName("channel")]
    public string? Channel { get; set; }
}
