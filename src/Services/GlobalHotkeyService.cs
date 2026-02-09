using Microsoft.Extensions.Logging;
using SharpHook;
using SharpHook.Data;
using StreamTalkerClient.Infrastructure.Logging;

namespace StreamTalkerClient.Services;

public class GlobalHotkeyService : IDisposable, IAsyncDisposable
{
    private readonly ILogger<GlobalHotkeyService> _logger;
    private TaskPoolGlobalHook? _hook;
    private Task? _hookTask;
    private bool _disposed;

    /// <summary>
    /// Key to skip current message (default: NumPad5)
    /// </summary>
    public KeyCode SkipCurrentKey { get; set; } = KeyCode.VcNumPad5;

    /// <summary>
    /// Key to skip all messages and clear queue (default: NumPad4)
    /// </summary>
    public KeyCode SkipAllKey { get; set; } = KeyCode.VcNumPad4;

    /// <summary>
    /// Enable/disable global hotkeys
    /// </summary>
    public bool Enabled { get; set; } = true;

    public event EventHandler? SkipCurrentPressed;
    public event EventHandler? SkipAllPressed;

    public GlobalHotkeyService()
    {
        _logger = AppLoggerFactory.CreateLogger<GlobalHotkeyService>();
    }

    public void Start()
    {
        if (_hook != null)
            return;

        try
        {
            _hook = new TaskPoolGlobalHook();
            _hook.KeyPressed += OnKeyPressed;

            _hookTask = _hook.RunAsync();
            _logger.LogInformation("Global hotkey service started. SkipCurrent: {SkipCurrent}, SkipAll: {SkipAll}",
                SkipCurrentKey, SkipAllKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start global hotkey service");
        }
    }

    public void Stop()
    {
        StopAsync().Wait(TimeSpan.FromSeconds(2));
    }

    public Task StopAsync()
    {
        if (_hook == null)
            return Task.CompletedTask;

        try
        {
            _hook.KeyPressed -= OnKeyPressed;

            var hook = _hook;
            _hook = null;
            _hookTask = null;

            // Dispose on background thread - don't block
            return Task.Run(() =>
            {
                try
                {
                    hook.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error disposing hook");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping global hotkey service");
            return Task.CompletedTask;
        }
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (!Enabled)
            return;

        if (e.Data.KeyCode == SkipCurrentKey)
        {
            _logger.LogDebug("Skip current hotkey pressed");
            SkipCurrentPressed?.Invoke(this, EventArgs.Empty);
        }
        else if (e.Data.KeyCode == SkipAllKey)
        {
            _logger.LogDebug("Skip all hotkey pressed");
            SkipAllPressed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        var task = StopAsync();
        return new ValueTask(task);
    }

    /// <summary>
    /// Get display name for a key code
    /// </summary>
    public static string GetKeyDisplayName(KeyCode keyCode)
    {
        return keyCode switch
        {
            KeyCode.VcNumPad0 => "NumPad 0",
            KeyCode.VcNumPad1 => "NumPad 1",
            KeyCode.VcNumPad2 => "NumPad 2",
            KeyCode.VcNumPad3 => "NumPad 3",
            KeyCode.VcNumPad4 => "NumPad 4",
            KeyCode.VcNumPad5 => "NumPad 5",
            KeyCode.VcNumPad6 => "NumPad 6",
            KeyCode.VcNumPad7 => "NumPad 7",
            KeyCode.VcNumPad8 => "NumPad 8",
            KeyCode.VcNumPad9 => "NumPad 9",
            KeyCode.VcF1 => "F1",
            KeyCode.VcF2 => "F2",
            KeyCode.VcF3 => "F3",
            KeyCode.VcF4 => "F4",
            KeyCode.VcF5 => "F5",
            KeyCode.VcF6 => "F6",
            KeyCode.VcF7 => "F7",
            KeyCode.VcF8 => "F8",
            KeyCode.VcF9 => "F9",
            KeyCode.VcF10 => "F10",
            KeyCode.VcF11 => "F11",
            KeyCode.VcF12 => "F12",
            KeyCode.VcPause => "Pause",
            KeyCode.VcScrollLock => "ScrollLock",
            KeyCode.VcInsert => "Insert",
            KeyCode.VcDelete => "Delete",
            KeyCode.VcHome => "Home",
            KeyCode.VcEnd => "End",
            KeyCode.VcPageUp => "PageUp",
            KeyCode.VcPageDown => "PageDown",
            _ => keyCode.ToString().Replace("Vc", "")
        };
    }

    /// <summary>
    /// Parse key code from string
    /// </summary>
    public static KeyCode ParseKeyCode(string name)
    {
        if (Enum.TryParse<KeyCode>(name, true, out var result))
            return result;

        if (Enum.TryParse<KeyCode>("Vc" + name, true, out result))
            return result;

        return KeyCode.VcUndefined;
    }
}
