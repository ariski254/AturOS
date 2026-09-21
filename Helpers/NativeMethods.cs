using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AturOS.Helpers;

/// <summary>
/// Win32 P/Invoke signatures with memory leak prevention, strict error handling,
/// and safe handle lifetime management.
/// </summary>
public static class NativeMethods
{
    public const uint PROCESS_SET_QUOTA = 0x0100;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

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

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("psapi.dll", SetLastError = true, ExactSpelling = true)]
    public static extern int EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

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
}
