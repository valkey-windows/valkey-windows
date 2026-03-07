using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ValkeyService.CommandLine;
using ValkeyService.Native;
using ValkeyService.Service;

namespace ValkeyService;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var result = CommandLineParser.Parse(args);

            return result switch
            {
                HelpCommand => PrintHelp(),
                VersionCommand => PrintVersion(),
                InstallCommand cmd => InstallService(cmd),
                UninstallCommand cmd => UninstallService(cmd),
                RunCommand cmd => await RunValkey(cmd),
                _ => PrintHelp()
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    #region Command Handling

    private static int PrintHelp()
    {
        CommandLineParser.PrintHelp();
        return 0;
    }

    private static int PrintVersion()
    {
        CommandLineParser.PrintVersion();
        return 0;
    }

    private static int InstallService(InstallCommand cmd)
    {
        var options = cmd.Options;

        Console.WriteLine($"Installing service '{options.ServiceName}'...");

        // Get current executable path
        var exePath = Environment.ProcessPath
            ?? AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + ".exe";

        // Build service arguments
        var serviceArgs = new List<string>();

        // Add run command (service mode)
        serviceArgs.Add("run");

        // Add config file argument
        serviceArgs.Add($"-c \"{Path.GetFullPath(options.ConfigFilePath)}\"");

        // Add other arguments
        if (options.Port.HasValue)
            serviceArgs.Add($"--port {options.Port.Value}");

        if (!string.IsNullOrEmpty(options.DataDirectory))
            serviceArgs.Add($"--dir \"{options.DataDirectory}\"");

        if (!string.IsNullOrEmpty(options.LogLevel))
            serviceArgs.Add($"--loglevel {options.LogLevel}");

        // Build full binary path
        var binaryPath = $"\"{exePath}\" {string.Join(" ", serviceArgs)}";

        try
        {
            ServiceManager.InstallService(
                options.ServiceName,
                binaryPath,
                options.DisplayName ?? "Valkey Server",
                options.Description ?? "Valkey in-memory data structure store",
                options.StartMode);

            Console.WriteLine($"Service '{options.ServiceName}' installed successfully.");
            Console.WriteLine();

            // Ask whether to start the service
            if (options.StartMode == "auto")
            {
                Console.WriteLine("Starting service...");
                try
                {
                    ServiceManager.StartServiceByName(options.ServiceName);
                    Console.WriteLine("Service started.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start service: {ex.Message}");
                    Console.WriteLine($"Please run manually: sc start {options.ServiceName}");
                }
            }
            else
            {
                Console.WriteLine($"Start the service with: sc start {options.ServiceName}");
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine("Please run this program as administrator.");
            return 1;
        }
    }

    private static int UninstallService(UninstallCommand cmd)
    {
        var serviceName = cmd.Options.ServiceName;

        Console.WriteLine($"Uninstalling service '{serviceName}'...");

        try
        {
            ServiceManager.UninstallService(serviceName);
            Console.WriteLine($"Service '{serviceName}' uninstalled successfully.");
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine("Please run this program as administrator.");
            return 1;
        }
    }

    private static async Task<int> RunValkey(RunCommand cmd)
    {
        var options = cmd.Options;

        // Build configuration
        var config = new ValkeyConfiguration
        {
            ConfigFilePath = options.ConfigFilePath,
            Port = options.Port,
            DataDirectory = options.DataDirectory,
            LogLevel = options.LogLevel
        };

        if (options.Foreground)
        {
            // Run in foreground
            return await RunForegroundAsync(config);
        }
        else
        {
            // Run as Windows service
            return await RunAsServiceAsync(config);
        }
    }

    private static async Task<int> RunForegroundAsync(ValkeyConfiguration config)
    {
        Console.WriteLine("Starting Valkey in foreground...");

        using var processManager = new ValkeyProcessManager(config);

        // Handle Ctrl+C
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nStopping...");
            cts.Cancel();
        };

        // Stop automatically when the process exits
        processManager.ProcessExited += (sender, e) =>
        {
            Console.WriteLine($"Valkey process exited (exit code: {e.ExitCode})");
            cts.Cancel();
        };

        try
        {
            var started = await processManager.StartAsync(cts.Token);
            if (!started)
            {
                Console.Error.WriteLine("Failed to start Valkey");
                return 1;
            }

            Console.WriteLine($"Valkey started (PID: {processManager.ProcessId})");
            Console.WriteLine("Press Ctrl+C to stop...");

            // Wait for cancellation
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected exit
        }
        finally
        {
            Console.WriteLine("Stopping Valkey...");
            await processManager.StopAsync();
        }

        return 0;
    }

    private static async Task<int> RunAsServiceAsync(ValkeyConfiguration config)
    {
        var host = Host.CreateDefaultBuilder()
            .UseWindowsService()
            .ConfigureLogging(logging =>
            {
#pragma warning disable CA1416 // Platform compatibility warning: AddEventLog is Windows-only
                logging.AddEventLog();
#pragma warning restore CA1416
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton(config);
                services.AddSingleton<ValkeyProcessManager>();
                services.AddHostedService<ValkeyBackgroundService>();
            })
            .Build();

        await host.RunAsync();
        return 0;
    }

    #endregion
}

/// <summary>
/// Valkey background service (Windows service mode)
/// </summary>
public class ValkeyBackgroundService : BackgroundService
{
    private readonly ValkeyConfiguration _config;
    private readonly ILogger<ValkeyBackgroundService> _logger;
    private readonly ValkeyProcessManager _processManager;

    public ValkeyBackgroundService(ValkeyConfiguration config, ValkeyProcessManager processManager, ILogger<ValkeyBackgroundService> logger)
    {
        _config = config;
        _processManager = processManager;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Valkey service...");

        _processManager.ProcessExited += OnProcessExited;

        var started = await _processManager.StartAsync(cancellationToken);
        if (!started)
        {
            _logger.LogError("Failed to start Valkey process");
            throw new InvalidOperationException("Unable to start Valkey process");
        }

        _logger.LogInformation("Valkey service started (PID: {ProcessId})", _processManager.ProcessId);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Keep the service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Valkey service...");

        _processManager.ProcessExited -= OnProcessExited;
        await _processManager.StopAsync(cancellationToken);

        _logger.LogInformation("Valkey service stopped");

        await base.StopAsync(cancellationToken);
    }

    private void OnProcessExited(object? sender, ProcessExitedEventArgs e)
    {
        _logger.LogWarning("Valkey process exited unexpectedly (exit code: {ExitCode})", e.ExitCode);
    }
}
