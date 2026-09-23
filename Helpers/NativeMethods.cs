using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AturOS.Helpers;

/// <summary>
/// Hardened Win32 & NT Native P/Invoke signatures with memory leak prevention,
/// strict error handling, kernel token privilege elevation, and resource lifetime management.
/// </summary>
public static class NativeMethods
{
    public const uint PROCESS_SET_QUOTA = 0x0100;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    private const int SystemMemoryListInformation = 80;
    private const int MemoryPurgeStandbyList = 4;
    private const int MemoryEmptyWorkingSets = 2;

    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const string SE_PROFILE_SINGLE_PROCESS_NAME = "SeProfileSingleProcessPrivilege";
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public static MEMORYSTATUSEX Create()
        {
            return new MEMORYSTATUSEX
            {
                dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>()
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus; // 0 = Offline, 1 = Online, 255 = Unknown
        public byte BatteryFlag;  // 1 = High, 2 = Low, 4 = Critical, 8 = Charging, 128 = No system battery, 255 = Unknown
        public byte BatteryLifePercent; // 0-100, 255 = Unknown
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privileges;
    }

    // --- kernel32.dll ---
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    public static extern IntPtr GetCurrentProcess();

    public const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MoveFileEx(string lpExistingFileName, string? lpNewFileName, uint dwFlags);

    // --- psapi.dll ---
    [DllImport("psapi.dll", SetLastError = true, ExactSpelling = true)]
    public static extern int EmptyWorkingSet(IntPtr hProcess);

    // --- advapi32.dll (Token Privilege Management) ---
    [DllImport("advapi32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    // --- ntdll.dll (Kernel Memory Management) ---
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetSystemInformation(int SystemInformationClass, ref int SystemInformation, int SystemInformationLength);

    /// <summary>
    /// Safely trims the working set of a target process using minimum required rights,
    /// guaranteeing the unmanaged handle is immediately closed to eliminate resource leaks.
    /// Handles Protected Processes (PPL) and access denied cleanly.
    /// </summary>
    public static bool SafeEmptyWorkingSet(int processId)
    {
        if (processId <= 4) return false; // System idle & kernel

        const uint accessRights = PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED_INFORMATION;
        IntPtr hProcess = OpenProcess(accessRights, false, processId);

        if (hProcess == IntPtr.Zero)
        {
            // Error 5 (ERROR_ACCESS_DENIED) is normal for Protected Process Light (PPL) / antivirus
            return false;
        }

        try
        {
            int result = EmptyWorkingSet(hProcess);
            return result != 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    /// <summary>
    /// Gets snapshot of physical and virtual system memory without heap allocations.
    /// </summary>
    public static bool TryGetMemoryStatus(out MEMORYSTATUSEX status)
    {
        status = MEMORYSTATUSEX.Create();
        return GlobalMemoryStatusEx(ref status);
    }

    /// <summary>
    /// Purges the Windows NT Standby List using kernel NtSetSystemInformation with
    /// SeProfileSingleProcessPrivilege elevation.
    /// Returns true if the kernel accepted the purge command.
    /// </summary>
    public static bool SafePurgeStandbyList()
    {
        try
        {
            if (EnablePrivilege(SE_PROFILE_SINGLE_PROCESS_NAME))
            {
                int command = MemoryPurgeStandbyList;
                int status = NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int));
                return status >= 0; // NT_SUCCESS (status >= 0)
            }
        }
        catch
        {
            // Fallback gracefully if NT native call is restricted
        }
        return false;
    }

    /// <summary>
    /// Enables a specific Windows security privilege on the current process token.
    /// </summary>
    private static bool EnablePrivilege(string privilegeName)
    {
        IntPtr hToken = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
            {
                return false;
            }

            if (!LookupPrivilegeValue(null, privilegeName, out LUID luid))
            {
                return false;
            }

            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                }
            };

            return AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (hToken != IntPtr.Zero)
            {
                CloseHandle(hToken);
            }
        }
    }
}
