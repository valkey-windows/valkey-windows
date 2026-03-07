namespace ValkeyService.CommandLine;

/// <summary>
/// Command-line parser
/// </summary>
public static class CommandLineParser
{
    /// <summary>
    /// Parse command-line arguments
    /// </summary>
    public static CommandResult Parse(string[] args)
    {
        if (args.Length == 0)
            return new HelpCommand();

        // Check help and version flags
        if (HasFlag(args, "-h", "--help"))
            return new HelpCommand();

        if (HasFlag(args, "-v", "--version"))
            return new VersionCommand();

        // Parse command
        var command = args[0].ToLowerInvariant();

        return command switch
        {
            "install" => ParseInstallCommand(args),
            "uninstall" => ParseUninstallCommand(args),
            "run" => ParseRunCommand(args, 1),
            _ => ParseRunCommand(args, 0) // Default to run command
        };
    }

    private static InstallCommand ParseInstallCommand(string[] args)
    {
        var options = new InstallOptions();
        ParseRunOptions(args, 1, options);

        // Parse install-specific options
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--service-name":
                    if (i + 1 < args.Length)
                    {
                        options.ServiceName = args[++i];
                    }
                    break;

                case "--display-name":
                    if (i + 1 < args.Length)
                    {
                        options.DisplayName = args[++i];
                    }
                    break;

                case "--description":
                    if (i + 1 < args.Length)
                    {
                        options.Description = args[++i];
                    }
                    break;

                case "--start-mode":
                    if (i + 1 < args.Length)
                    {
                        options.StartMode = args[++i].ToLowerInvariant();
                    }
                    break;
            }
        }

        return new InstallCommand(options);
    }

    private static UninstallCommand ParseUninstallCommand(string[] args)
    {
        var options = new UninstallOptions();

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--service-name":
                    if (i + 1 < args.Length)
                    {
                        options.ServiceName = args[++i];
                    }
                    break;
            }
        }

        return new UninstallCommand(options);
    }

    private static RunCommand ParseRunCommand(string[] args, int startIndex)
    {
        var options = new RunOptions();
        ParseRunOptions(args, startIndex, options);
        options.AsService = !options.Foreground;
            return new RunCommand(options);
    }

    private static void ParseRunOptions(string[] args, int startIndex, RunOptions options)
    {
        for (int i = startIndex; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "-c":
                case "--config":
                    if (i + 1 < args.Length)
                    {
                        options.ConfigFilePath = args[++i];
                    }
                    break;

                case "--port":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var port))
                    {
                        options.Port = port;
                        i++;
                    }
                    break;

                case "--dir":
                    if (i + 1 < args.Length)
                    {
                        options.DataDirectory = args[++i];
                    }
                    break;

                case "--loglevel":
                    if (i + 1 < args.Length)
                    {
                        options.LogLevel = args[++i];
                    }
                    break;

                case "-f":
                case "--foreground":
                    options.Foreground = true;
                    break;
            }
        }
    }

    private static bool HasFlag(string[] args, params string[] flags)
    {
        return args.Any(arg => flags.Contains(arg, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Print help text
    /// </summary>
    public static void PrintHelp()
    {
        Console.WriteLine(@"
ValkeyService - Valkey Windows Service Wrapper

Usage: ValkeyService [command] [options]

Commands:
  install       Install as a Windows service
  uninstall     Uninstall the Windows service
  run           Run Valkey (default command)

Options:
  -c, --config <FILE>      Valkey config file path (default: valkey.conf)
  --port <PORT>            Override Valkey port
  --dir <DIRECTORY>        Override Valkey data directory
  --loglevel <LEVEL>       Log level (debug, verbose, notice, warning)
  -f, --foreground         Run in foreground
  --service-name <NAME>    Service name (default: Valkey)
  --display-name <NAME>    Service display name
  --description <TEXT>     Service description
  --start-mode <MODE>      Start mode: auto, manual (default: auto)
  -h, --help               Show help
  -v, --version            Show version

Examples:
  ValkeyService.exe install -c valkey.conf --port 6380
  ValkeyService.exe run --foreground
  ValkeyService.exe uninstall
  ValkeyService.exe uninstall --service-name MyValkey
");
    }

    /// <summary>
    /// Print version information
    /// </summary>
    public static void PrintVersion()
    {
        var version = typeof(CommandLineParser).Assembly.GetName().Version;
        Console.WriteLine($"ValkeyService version {version?.ToString() ?? "1.0.0"}");
        Console.WriteLine("Valkey Windows Service Wrapper");
        Console.WriteLine("https://github.com/valkey-windows/valkey-windows");
    }
}
