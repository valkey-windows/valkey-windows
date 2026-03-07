namespace ValkeyService.CommandLine;

/// <summary>
/// Command types
/// </summary>
public enum CommandType
{
    /// <summary>
    /// Show help
    /// </summary>
    Help,

    /// <summary>
    /// Show version
    /// </summary>
    Version,

    /// <summary>
    /// Install service
    /// </summary>
    Install,

    /// <summary>
    /// Uninstall service
    /// </summary>
    Uninstall,

    /// <summary>
    /// Run Valkey
    /// </summary>
    Run
}

/// <summary>
/// Base class for parsed command results
/// </summary>
public abstract class CommandResult(CommandType type)
{
    public CommandType Type { get; } = type;
}

/// <summary>
/// Help command result
/// </summary>
public class HelpCommand : CommandResult
{
    public HelpCommand() : base(CommandType.Help) { }
}

/// <summary>
/// Version command result
/// </summary>
public class VersionCommand : CommandResult
{
    public VersionCommand() : base(CommandType.Version) { }
}

/// <summary>
/// Options for running Valkey
/// </summary>
public class RunOptions
{
    /// <summary>
    /// Config file path
    /// </summary>
    public string ConfigFilePath { get; set; } = "valkey.conf";

    /// <summary>
    /// Valkey port
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Data directory
    /// </summary>
    public string? DataDirectory { get; set; }

    /// <summary>
    /// Log level
    /// </summary>
    public string? LogLevel { get; set; }

    /// <summary>
    /// Run in foreground
    /// </summary>
    public bool Foreground { get; set; }

    /// <summary>
    /// Run as a Windows service
    /// </summary>
    public bool AsService { get; set; }
}

/// <summary>
/// Run command result
/// </summary>
public class RunCommand : CommandResult
{
    public RunOptions Options { get; }

    public RunCommand(RunOptions options) : base(CommandType.Run)
    {
        Options = options;
    }
}

/// <summary>
/// Install options
/// </summary>
public class InstallOptions : RunOptions
{
    /// <summary>
    /// Service name
    /// </summary>
    public string ServiceName { get; set; } = "Valkey";

    /// <summary>
    /// Service display name
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Service description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Start mode: auto, manual, disabled
    /// </summary>
    public string StartMode { get; set; } = "auto";
}

/// <summary>
/// Install command result
/// </summary>
public class InstallCommand : CommandResult
{
    public InstallOptions Options { get; }

    public InstallCommand(InstallOptions options) : base(CommandType.Install)
    {
        Options = options;
    }
}

/// <summary>
/// Uninstall options
/// </summary>
public class UninstallOptions
{
    /// <summary>
    /// Service name
    /// </summary>
    public string ServiceName { get; set; } = "Valkey";
}

/// <summary>
/// Uninstall command result
/// </summary>
public class UninstallCommand : CommandResult
{
    public UninstallOptions Options { get; }

    public UninstallCommand(UninstallOptions options) : base(CommandType.Uninstall)
    {
        Options = options;
    }
}
