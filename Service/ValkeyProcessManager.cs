using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ValkeyService.Service;

/// <summary>
/// Valkey process manager
/// </summary>
public class ValkeyProcessManager : IDisposable
{
    private Process? _valkeyProcess;
    private readonly ValkeyConfiguration _config;
    private readonly ILogger<ValkeyProcessManager>? _logger;
    private readonly SemaphoreSlim _startLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Process exit event
    /// </summary>
    public event EventHandler<ProcessExitedEventArgs>? ProcessExited;

    public ValkeyProcessManager(ValkeyConfiguration config, ILogger<ValkeyProcessManager>? logger = null)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Whether the process is running
    /// </summary>
    public bool IsRunning => _valkeyProcess != null && !_valkeyProcess.HasExited;

    /// <summary>
    /// Process ID
    /// </summary>
    public int? ProcessId => _valkeyProcess?.Id;

    /// <summary>
    /// Start the Valkey process
    /// </summary>
    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        await _startLock.WaitAsync(cancellationToken);
        try
        {
            if (_valkeyProcess != null && !_valkeyProcess.HasExited)
            {
                _logger?.LogWarning("Valkey process is already running");
                return true;
            }

            var basePath = AppContext.BaseDirectory;
            var valkeyServerPath = Path.Combine(basePath, "valkey-server.exe");

            if (!File.Exists(valkeyServerPath))
            {
                _logger?.LogError("valkey-server.exe not found: {Path}", valkeyServerPath);
                return false;
            }

            var arguments = _config.BuildArguments();
            _logger?.LogInformation("Starting Valkey: {Path} {Args}", valkeyServerPath, arguments);

            var startInfo = new ProcessStartInfo(valkeyServerPath, arguments)
            {
                WorkingDirectory = basePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _valkeyProcess = Process.Start(startInfo);

            if (_valkeyProcess == null)
            {
                _logger?.LogError("Failed to start Valkey process");
                return false;
            }

            // Enable event handling
            _valkeyProcess.EnableRaisingEvents = true;
            _valkeyProcess.Exited += OnProcessExited;
            _valkeyProcess.OutputDataReceived += OnOutputDataReceived;
            _valkeyProcess.ErrorDataReceived += OnErrorDataReceived;

            // Start reading output asynchronously
            _valkeyProcess.BeginOutputReadLine();
            _valkeyProcess.BeginErrorReadLine();

            // Brief wait to confirm the process has started
            await Task.Delay(100, cancellationToken);

            if (_valkeyProcess.HasExited)
            {
                _logger?.LogError("Valkey process exited immediately, exit code: {ExitCode}", _valkeyProcess.ExitCode);
                return false;
            }

            _logger?.LogInformation("Valkey process started, PID: {ProcessId}", _valkeyProcess.Id);
            return true;
        }
        finally
        {
            _startLock.Release();
        }
    }

    /// <summary>
    /// Stop the Valkey process
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_valkeyProcess == null || _valkeyProcess.HasExited)
        {
            _logger?.LogDebug("Valkey process is not running");
            return;
        }

        _logger?.LogInformation("Stopping Valkey process...");

        // Attempt graceful shutdown
        await TryGracefulShutdownAsync(cancellationToken);

        // Optimized async wait
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_config.GracefulShutdownTimeoutMs);

        try
        {
            await WaitForExitAsync(_valkeyProcess, cts.Token);
            _logger?.LogInformation("Valkey process shut down gracefully");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Graceful shutdown timed out; terminating process");
            try
            {
                _valkeyProcess.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to terminate process forcibly");
            }
        }

        CleanupProcess();
    }

    /// <summary>
    /// Attempt graceful shutdown via valkey-cli
    /// </summary>
    private async Task TryGracefulShutdownAsync(CancellationToken cancellationToken)
    {
        var basePath = AppContext.BaseDirectory;
        var valkeyCliPath = Path.Combine(basePath, "valkey-cli.exe");

        if (!File.Exists(valkeyCliPath))
        {
            _logger?.LogWarning("valkey-cli.exe not found; skipping graceful shutdown");
            return;
        }

        // Build full valkey-cli arguments (includes config path and dir)
        // Ensures valkey-cli uses the correct data directory for persistence
        var args = _config.BuildCliShutdownArguments();
        _logger?.LogDebug("Running valkey-cli {Args}", args);

        try
        {
            using var cli = Process.Start(new ProcessStartInfo(valkeyCliPath, args)
            {
                WorkingDirectory = basePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (cli != null)
            {
                // Apply timeout to avoid waiting indefinitely
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                await cli.WaitForExitAsync(cts.Token);
                _logger?.LogDebug("Sent SHUTDOWN command, exit code: {ExitCode}", cli.ExitCode);
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("valkey-cli SHUTDOWN command timed out");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to send SHUTDOWN command");
        }
    }

    /// <summary>
    /// Asynchronously wait for process exit (non-blocking)
    /// </summary>
    private static Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
    {
        if (process.HasExited)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<bool>();

        void OnExited(object? sender, EventArgs e)
        {
            process.Exited -= OnExited;
            tcs.TrySetResult(true);
        }

        process.Exited += OnExited;

        // Handle already exited
        if (process.HasExited)
        {
            process.Exited -= OnExited;
            return Task.CompletedTask;
        }

        // Register cancellation
        cancellationToken.Register(() =>
        {
            process.Exited -= OnExited;
            tcs.TrySetCanceled(cancellationToken);
        });

        return tcs.Task;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        var process = sender as Process;
        var exitCode = process?.ExitCode ?? -1;
        _logger?.LogWarning("Valkey process exited unexpectedly, exit code: {ExitCode}", exitCode);

        ProcessExited?.Invoke(this, new ProcessExitedEventArgs(exitCode, process?.StartTime ?? DateTime.MinValue, DateTime.Now));

        CleanupProcess();
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            _logger?.LogInformation("[Valkey] {Data}", e.Data);
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            _logger?.LogError("[Valkey] {Data}", e.Data);
        }
    }

    private void CleanupProcess()
    {
        if (_valkeyProcess != null)
        {
            try
            {
                _valkeyProcess.Exited -= OnProcessExited;
                _valkeyProcess.OutputDataReceived -= OnOutputDataReceived;
                _valkeyProcess.ErrorDataReceived -= OnErrorDataReceived;
                _valkeyProcess.Dispose();
            }
            catch { }
            _valkeyProcess = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CleanupProcess();
        _startLock.Dispose();
    }
}

/// <summary>
/// Process exit event args
/// </summary>
public class ProcessExitedEventArgs(int exitCode, DateTime startTime, DateTime exitTime) : EventArgs
{
    public int ExitCode { get; } = exitCode;
    public DateTime StartTime { get; } = startTime;
    public DateTime ExitTime { get; } = exitTime;
    public TimeSpan Duration => ExitTime - StartTime;
}
