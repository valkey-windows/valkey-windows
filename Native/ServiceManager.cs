using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ValkeyService.Native;

/// <summary>
/// Windows service manager (via P/Invoke)
/// </summary>
public static class ServiceManager
{
    #region P/Invoke declarations

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenSCManager(
        string? lpMachineName,
        string? lpDatabaseName,
        uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateService(
        IntPtr hSCManager,
        string lpServiceName,
        string? lpDisplayName,
        uint dwDesiredAccess,
        uint dwServiceType,
        uint dwStartType,
        uint dwErrorControl,
        string lpBinaryPathName,
        string? lpLoadOrderGroup,
        string? lpdwTagId,
        string? lpDependencies,
        string? lpServiceStartName,
        string? lpPassword);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenService(
        IntPtr hSCManager,
        string lpServiceName,
        uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DeleteService(IntPtr hService);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool StartService(
        IntPtr hService,
        uint dwNumServiceArgs,
        string? lpServiceArgVectors);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool ControlService(
        IntPtr hService,
        uint dwControl,
        ref SERVICE_STATUS lpServiceStatus);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatus(
        IntPtr hService,
        ref SERVICE_STATUS lpServiceStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SERVICE_STATUS
    {
        public uint dwServiceType;
        public uint dwCurrentState;
        public uint dwControlsAccepted;
        public uint dwWin32ExitCode;
        public uint dwServiceSpecificExitCode;
        public uint dwCheckPoint;
        public uint dwWaitHint;
    }

    #endregion

    #region Constants

    // Access rights
    private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
    private const uint SERVICE_ALL_ACCESS = 0xF01FF;

    // Service types
    private const uint SERVICE_WIN32_OWN_PROCESS = 0x10;

    // Start types
    private const uint SERVICE_AUTO_START = 0x2;
    private const uint SERVICE_DEMAND_START = 0x3;
    private const uint SERVICE_DISABLED = 0x4;

    // Error control
    private const uint SERVICE_ERROR_NORMAL = 0x1;

    // Service control
    private const uint SERVICE_CONTROL_STOP = 0x1;

    // Service states
    private const uint SERVICE_STOPPED = 0x1;
    private const uint SERVICE_START_PENDING = 0x2;
    private const uint SERVICE_STOP_PENDING = 0x3;
    private const uint SERVICE_RUNNING = 0x4;

    // Error codes
    private const int ERROR_SERVICE_EXISTS = 1073;
    private const int ERROR_SERVICE_DOES_NOT_EXIST = 1060;
    private const int ERROR_SERVICE_MARKED_FOR_DELETE = 1072;

    #endregion

    /// <summary>
    /// Install a Windows service
    /// </summary>
    public static void InstallService(
        string serviceName,
        string binaryPath,
        string? displayName = null,
        string? description = null,
        string startMode = "auto")
    {
        var scManager = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
        if (scManager == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open service manager; please run as administrator");
        }

        try
        {
            var startType = startMode.ToLowerInvariant() switch
            {
                "auto" => SERVICE_AUTO_START,
                "manual" => SERVICE_DEMAND_START,
                "disabled" => SERVICE_DISABLED,
                _ => SERVICE_AUTO_START
            };

            var service = CreateService(
                scManager,
                serviceName,
                displayName ?? serviceName,
                SERVICE_ALL_ACCESS,
                SERVICE_WIN32_OWN_PROCESS,
                startType,
                SERVICE_ERROR_NORMAL,
                binaryPath,
                null,
                null,
                null,
                null,
                null);

            if (service == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                if (error == ERROR_SERVICE_EXISTS)
                {
                    throw new InvalidOperationException($"Service '{serviceName}' already exists");
                }
                throw new Win32Exception(error, "Failed to create service");
            }

            try
            {
                // Set service description (optional)
                if (!string.IsNullOrEmpty(description))
                {
                    SetServiceDescription(service, description);
                }
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(scManager);
        }
    }

    /// <summary>
    /// Uninstall a Windows service
    /// </summary>
    public static void UninstallService(string serviceName)
    {
        var scManager = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
        if (scManager == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open service manager; please run as administrator");
        }

        try
        {
            var service = OpenService(scManager, serviceName, SERVICE_ALL_ACCESS);
            if (service == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                if (error == ERROR_SERVICE_DOES_NOT_EXIST)
                {
                    throw new InvalidOperationException($"Service '{serviceName}' does not exist");
                }
                throw new Win32Exception(error, "Failed to open service");
            }

            try
            {
                // Attempt to stop the service first
                StopService(service);

                if (!DeleteService(service))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == ERROR_SERVICE_MARKED_FOR_DELETE)
                    {
                        throw new InvalidOperationException($"Service '{serviceName}' is marked for deletion; please reboot to complete removal");
                    }
                    throw new Win32Exception(error, "Failed to delete service");
                }
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(scManager);
        }
    }

    /// <summary>
    /// Start a service
    /// </summary>
    public static void StartServiceByName(string serviceName)
    {
        var scManager = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
        if (scManager == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open service manager");
        }

        try
        {
            var service = OpenService(scManager, serviceName, SERVICE_ALL_ACCESS);
            if (service == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to open service");
            }

            try
            {
                if (!StartService(service, 0, null))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to start service");
                }
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(scManager);
        }
    }

    /// <summary>
    /// Stop a service
    /// </summary>
    private static void StopService(IntPtr service)
    {
        var status = new SERVICE_STATUS();

        if (!ControlService(service, SERVICE_CONTROL_STOP, ref status))
        {
            var error = Marshal.GetLastWin32Error();
            // Service may already be stopped
            if (error != 1062) // ERROR_SERVICE_NOT_ACTIVE
            {
                // Ignore stop failure, continue trying to delete
            }
        }

        // Wait for the service to stop
        for (int i = 0; i < 30; i++)
        {
            if (!QueryServiceStatus(service, ref status))
                break;

            if (status.dwCurrentState == SERVICE_STOPPED)
                return;

            Thread.Sleep(500);
        }
    }

    /// <summary>
    /// Set service description
    /// </summary>
    private static void SetServiceDescription(IntPtr service, string description)
    {
        // Use ChangeServiceConfig2 to set description
        // Simplified here; description not set
        // A full implementation requires additional P/Invoke declarations
    }

    /// <summary>
    /// Check whether a service exists
    /// </summary>
    public static bool ServiceExists(string serviceName)
    {
        var scManager = OpenSCManager(null, null, SC_MANAGER_ALL_ACCESS);
        if (scManager == IntPtr.Zero)
            return false;

        try
        {
            var service = OpenService(scManager, serviceName, SERVICE_ALL_ACCESS);
            if (service == IntPtr.Zero)
                return false;

            CloseServiceHandle(service);
            return true;
        }
        finally
        {
            CloseServiceHandle(scManager);
        }
    }
}
