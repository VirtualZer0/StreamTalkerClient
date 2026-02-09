using System.Reflection;

namespace StreamTalkerClient.Infrastructure;

/// <summary>
/// Utility for extracting embedded resources from the assembly.
/// </summary>
public static class EmbeddedResourceHelper
{
    /// <summary>Extract an embedded resource to the specified file path.</summary>
    /// <param name="resourceName">Full resource name (e.g., "StreamTalkerClient.Resources.docker-compose.yml")</param>
    /// <param name="outputPath">Where to write the extracted file</param>
    public static void ExtractResource(string resourceName, string outputPath)
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found in assembly.");
        }

        // Ensure directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var fileStream = File.Create(outputPath);
        stream.CopyTo(fileStream);
    }

    /// <summary>Get full resource name for a file in Resources folder.</summary>
    public static string GetResourceName(string fileName)
    {
        return $"StreamTalkerClient.Resources.{fileName}";
    }

    /// <summary>Check if a resource exists in the assembly.</summary>
    public static bool ResourceExists(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        return resourceNames.Contains(resourceName);
    }
}
