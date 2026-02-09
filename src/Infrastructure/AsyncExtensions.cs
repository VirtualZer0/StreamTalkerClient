using Microsoft.Extensions.Logging;

namespace StreamTalkerClient.Infrastructure;

/// <summary>
/// Extension methods for safe async execution with standardized error handling.
/// </summary>
public static class AsyncExtensions
{
    /// <summary>
    /// Executes an async operation safely, logging errors and returning a fallback value on failure.
    /// Rethrows OperationCanceledException to preserve cancellation semantics.
    /// </summary>
    public static async Task<T?> ExecuteSafeAsync<T>(
        this Task<T> task,
        ILogger logger,
        string operationName,
        T? fallback = default) where T : class
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to {Operation}", operationName);
            return fallback;
        }
    }

    /// <summary>
    /// Executes an async boolean operation safely, returning false on failure.
    /// Rethrows OperationCanceledException to preserve cancellation semantics.
    /// </summary>
    public static async Task<bool> ExecuteSafeAsync(
        this Task<bool> task,
        ILogger logger,
        string operationName)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to {Operation}", operationName);
            return false;
        }
    }

    /// <summary>
    /// Executes an async int operation safely, returning a fallback value on failure.
    /// Rethrows OperationCanceledException to preserve cancellation semantics.
    /// </summary>
    public static async Task<int> ExecuteSafeAsync(
        this Task<int> task,
        ILogger logger,
        string operationName,
        int fallback = 0)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to {Operation}", operationName);
            return fallback;
        }
    }

    /// <summary>
    /// Executes an async void operation safely, logging errors on failure.
    /// Rethrows OperationCanceledException to preserve cancellation semantics.
    /// </summary>
    public static async Task ExecuteSafeAsync(
        this Task task,
        ILogger logger,
        string operationName)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to {Operation}", operationName);
        }
    }

    /// <summary>
    /// Executes an async operation with retry logic and exponential backoff.
    /// </summary>
    public static async Task<T?> ExecuteWithRetryAsync<T>(
        this Func<Task<T>> operation,
        ILogger logger,
        string operationName,
        int maxRetries = 3,
        int baseDelayMs = 1000,
        CancellationToken ct = default) where T : class
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var isLastAttempt = attempt == maxRetries - 1;
                if (isLastAttempt)
                {
                    logger.LogError(ex, "Failed to {Operation} after {Attempts} attempts", operationName, maxRetries);
                    return null;
                }

                var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt));
                logger.LogWarning(ex, "Failed to {Operation}, attempt {Attempt}/{Max}. Retrying in {Delay}ms",
                    operationName, attempt + 1, maxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }

        return null;
    }
}
