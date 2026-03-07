using System.Text;

namespace ValkeyService.Service;

/// <summary>
/// Valkey service configuration
/// </summary>
public class ValkeyConfiguration
{
    /// <summary>
    /// Valkey config file path
    /// </summary>
    public string ConfigFilePath { get; init; } = "valkey.conf";

    /// <summary>
    /// Valkey port (overrides config file)
    /// </summary>
    public int? Port { get; init; }

    /// <summary>
    /// Valkey data directory (overrides config file)
    /// </summary>
    public string? DataDirectory { get; init; }

    /// <summary>
    /// Log level (overrides config file)
    /// </summary>
    public string? LogLevel { get; init; }

    /// <summary>
    /// Graceful shutdown timeout in milliseconds
    /// </summary>
    public int GracefulShutdownTimeoutMs { get; init; } = 5000;

    /// <summary>
    /// Process start timeout in milliseconds
    /// </summary>
    public int ProcessStartTimeoutMs { get; init; } = 3000;

    /// <summary>
    /// Convert a Windows path to a Cygwin/MSYS2 style path
    /// </summary>
    /// <param name="windowsPath">Windows path</param>
    /// <returns>Cygwin-style path (e.g., /cygdrive/c/path)</returns>
    public static string ToCygwinPath(string windowsPath)
    {
        var path = Path.GetFullPath(windowsPath);
        var colonIndex = path.IndexOf(':');
        if (colonIndex > 0)
        {
            var drive = path[..colonIndex].ToLower();
            return path
                .Remove(0, colonIndex + 1)
                .Insert(0, $"/cygdrive/{drive}")
                .Replace('\\', '/');
        }
        return path.Replace('\\', '/');
    }

    /// <summary>
    /// Get the config path in Cygwin format
    /// </summary>
    public string GetCygwinConfigPath()
    {
        return ToCygwinPath(ConfigFilePath);
    }

    /// <summary>
    /// Get the data directory path in Cygwin format
    /// </summary>
    public string? GetCygwinDataDirectory()
    {
        return DataDirectory != null ? ToCygwinPath(DataDirectory) : null;
    }

    /// <summary>
    /// Build valkey-server command-line arguments
    /// </summary>
    public string BuildArguments()
    {
        var args = new StringBuilder();

        // Config file (required for Cygwin path)
        args.Append($"\"{GetCygwinConfigPath()}\"");

        // Override options
        if (Port.HasValue)
            args.Append($" --port {Port.Value}");

        if (!string.IsNullOrEmpty(DataDirectory))
            args.Append($" --dir \"{GetCygwinDataDirectory()}\"");

        if (!string.IsNullOrEmpty(LogLevel))
            args.Append($" --loglevel {LogLevel}");

        return args.ToString();
    }

    /// <summary>
    /// Build valkey-cli SHUTDOWN command-line arguments
    /// </summary>
    public string BuildCliShutdownArguments()
    {
        var args = new StringBuilder();

        // Pass config path (ensures valkey-cli uses correct dir)
        args.Append($"\"{GetCygwinConfigPath()}\"");

        // Pass port override
        if (Port.HasValue)
            args.Append($" -p {Port.Value}");

        // Pass dir override
        if (!string.IsNullOrEmpty(DataDirectory))
            args.Append($" --dir \"{GetCygwinDataDirectory()}\"");

        // SHUTDOWN command
        args.Append(" SHUTDOWN");

        return args.ToString();
    }
}
