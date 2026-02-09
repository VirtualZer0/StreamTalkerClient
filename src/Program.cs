using Avalonia;
using StreamTalkerClient.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Runtime;

namespace StreamTalkerClient;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLoggerFactory.Initialize();

        try
        {
            // Start GC optimization timer (fire-and-forget with error handling)
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait for app initialization to complete
                    await Task.Delay(5000);

                    // Compact Large Object Heap and collect garbage
                    GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(2, GCCollectionMode.Optimized, blocking: false);

                    var logger = AppLoggerFactory.CreateLogger<Program>();
                    logger.LogInformation("Post-startup GC completed. Memory optimized.");
                }
                catch (Exception ex)
                {
                    var logger = AppLoggerFactory.CreateLogger<Program>();
                    logger.LogWarning(ex, "Post-startup GC optimization failed");
                }
            });

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            AppLoggerFactory.Shutdown();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .WithInterFont()
            .LogToTrace();

        // Enable Vulkan rendering on Windows for better GPU performance
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // Configure Win32 platform with Vulkan as preferred rendering mode
                // Fallback chain: Vulkan → WGL (OpenGL) → Software
                builder = builder
                    .With(new Win32PlatformOptions
                    {
                        RenderingMode = new[]
                        {
                            Win32RenderingMode.Vulkan,
                            Win32RenderingMode.Wgl,
                            Win32RenderingMode.Software
                        }
                    })
                    .UsePlatformDetect();

                var logger = AppLoggerFactory.CreateLogger<Program>();
                logger.LogInformation("Vulkan rendering enabled (fallback: WGL → Software)");
            }
            catch (Exception ex)
            {
                var logger = AppLoggerFactory.CreateLogger<Program>();
                logger.LogWarning(ex, "Failed to configure Vulkan, using default platform settings");
                builder = builder.UsePlatformDetect();
            }
        }
        else
        {
            builder = builder.UsePlatformDetect();
        }

        return builder;
    }
}
