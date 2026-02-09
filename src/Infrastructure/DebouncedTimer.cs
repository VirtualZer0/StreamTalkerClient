using Microsoft.Extensions.Logging;

namespace StreamTalkerClient.Infrastructure;

/// <summary>
/// A timer that prevents overlapping executions and waits the interval AFTER operation completion.
/// Unlike System.Windows.Forms.Timer which fires on fixed intervals, this ensures:
/// 1. Only one instance of the action runs at a time (deduplication)
/// 2. The interval is measured from when the previous execution completes
/// </summary>
public sealed class DebouncedTimer : IDisposable
{
    private readonly Func<Task> _action;
    private readonly TimeSpan _interval;
    private readonly ILogger _logger;
    private readonly string _name;

    private int _isRunning; // 0 = not running, 1 = running (for Interlocked)
    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Creates a new debounced timer.
    /// </summary>
    /// <param name="action">The async action to execute.</param>
    /// <param name="interval">The interval to wait AFTER each execution completes.</param>
    /// <param name="name">A name for logging purposes.</param>
    /// <param name="logger">Logger instance.</param>
    public DebouncedTimer(Func<Task> action, TimeSpan interval, string name, ILogger logger)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _interval = interval;
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets whether the timer is currently running its loop.
    /// </summary>
    public bool IsStarted => _cts != null && !_cts.IsCancellationRequested;

    /// <summary>
    /// Gets whether the action is currently executing.
    /// </summary>
    public bool IsExecuting => _isRunning == 1;

    /// <summary>
    /// Starts the timer loop. Does nothing if already started.
    /// </summary>
    public void Start()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DebouncedTimer));

        if (_cts != null)
            return; // Already started

        _cts = new CancellationTokenSource();
        _ = RunLoopAsync(_cts.Token);
        _logger.LogDebug("{Timer} started with interval {Interval}ms", _name, _interval.TotalMilliseconds);
    }

    /// <summary>
    /// Stops the timer loop. The currently executing action will complete.
    /// </summary>
    public void Stop()
    {
        if (_cts == null)
            return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
        _logger.LogDebug("{Timer} stopped", _name);
    }

    /// <summary>
    /// Triggers an immediate execution if not already running.
    /// Does not affect the timer loop schedule.
    /// </summary>
    /// <returns>True if execution was triggered, false if already executing.</returns>
    public async Task<bool> TriggerNowAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DebouncedTimer));

        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) == 1)
        {
            _logger.LogDebug("{Timer} trigger skipped - already executing", _name);
            return false;
        }

        try
        {
            await ExecuteActionAsync().ConfigureAwait(false);
            return true;
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        // Initial delay before first execution (optional, can be removed if immediate start is desired)
        try
        {
            await Task.Delay(_interval, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            // Try to acquire the running lock
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) == 1)
            {
                // Another execution is in progress (e.g., from TriggerNowAsync)
                // Wait a bit and try again
                try
                {
                    await Task.Delay(100, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                continue;
            }

            try
            {
                await ExecuteActionAsync().ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }

            // Wait interval AFTER completion
            try
            {
                await Task.Delay(_interval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ExecuteActionAsync()
    {
        try
        {
            await _action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown, don't log as error
            _logger.LogDebug("{Timer} action was cancelled", _name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Timer} action failed", _name);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }
}
