using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace StreamTalkerClient.Infrastructure.Logging;

public static class AppLoggerFactory
{
    private static ILoggerFactory? _loggerFactory;
    private static readonly object _lock = new();

    public static void Initialize(string logDirectory = "data/logs")
    {
        lock (_lock)
        {
            if (_loggerFactory != null)
                return;

            Directory.CreateDirectory(logDirectory);

            var logFilePath = Path.Combine(logDirectory, "app-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
#if DEBUG
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
#endif
                .CreateLogger();

            _loggerFactory = new SerilogLoggerFactory(Log.Logger);
        }
    }

    public static ILogger<T> CreateLogger<T>()
    {
        if (_loggerFactory == null)
            throw new InvalidOperationException("Logger factory not initialized. Call Initialize() first.");

        return _loggerFactory.CreateLogger<T>();
    }

    public static Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
    {
        if (_loggerFactory == null)
            throw new InvalidOperationException("Logger factory not initialized. Call Initialize() first.");

        return _loggerFactory.CreateLogger(categoryName);
    }

    public static void Shutdown()
    {
        Log.CloseAndFlush();
        _loggerFactory?.Dispose();
        _loggerFactory = null;
    }
}
