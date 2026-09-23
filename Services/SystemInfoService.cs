using System.IO;
using AturOS.Helpers;
using AturOS.Models;
using Microsoft.Win32;

namespace AturOS.Services;

public class SystemInfoService
{
    private long _prevIdleTime;
    private long _prevKernelTime;
    private long _prevUserTime;
    private bool _isFirstCpuSample = true;

    public SystemMetrics GetMetrics()
    {
        var metrics = new SystemMetrics();

        // 1. CPU Usage via GetSystemTimes
        metrics.CpuUsagePercent = CalculateCpuUsage();

        // 2. RAM Usage via GlobalMemoryStatusEx
        try
        {
            if (NativeMethods.TryGetMemoryStatus(out var memStatus))
            {
                metrics.TotalRamGb = Math.Round((double)memStatus.ullTotalPhys / (1024 * 1024 * 1024), 2);
                metrics.FreeRamGb = Math.Round((double)memStatus.ullAvailPhys / (1024 * 1024 * 1024), 2);
                metrics.UsedRamGb = Math.Round(metrics.TotalRamGb - metrics.FreeRamGb, 2);
                metrics.RamUsagePercent = Math.Round(memStatus.dwMemoryLoad * 1.0, 1);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal membaca status memori: {ex.Message}");
        }

        // 3. Storage C:\
        try
        {
            var drive = new DriveInfo("C");
            if (drive.IsReady)
            {
                metrics.SystemDriveLetter = drive.Name;
                metrics.TotalDriveGb = Math.Round((double)drive.TotalSize / (1024 * 1024 * 1024), 1);
                metrics.FreeDriveGb = Math.Round((double)drive.TotalFreeSpace / (1024 * 1024 * 1024), 1);
                metrics.UsedDriveGb = Math.Round(metrics.TotalDriveGb - metrics.FreeDriveGb, 1);
                if (metrics.TotalDriveGb > 0)
                {
                    metrics.DriveUsagePercent = Math.Round((metrics.UsedDriveGb / metrics.TotalDriveGb) * 100, 1);
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal membaca status drive C:\\: {ex.Message}");
        }

        // 4. OS Info & Metadata
        metrics.IsAdministrator = AdministratorHelper.IsAdministrator;
        metrics.ComputerName = Environment.MachineName;
        metrics.UserName = Environment.UserName;
        metrics.Architecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";

        var (osName, buildNum) = GetOperatingSystemDetails();
        metrics.OsName = osName;
        metrics.OsBuild = buildNum;

        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        metrics.UptimeFormatted = $"{(int)uptime.TotalHours} jam {uptime.Minutes} menit";

        return metrics;
    }

    private double CalculateCpuUsage()
    {
        try
        {
            if (!NativeMethods.GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
                return 0.0;

            if (_isFirstCpuSample)
            {
                _prevIdleTime = idleTime;
                _prevKernelTime = kernelTime;
                _prevUserTime = userTime;
                _isFirstCpuSample = false;
                return 0.0;
            }

            var diffIdle = idleTime - _prevIdleTime;
            var diffKernel = kernelTime - _prevKernelTime;
            var diffUser = userTime - _prevUserTime;

            _prevIdleTime = idleTime;
            _prevKernelTime = kernelTime;
            _prevUserTime = userTime;

            var totalSys = diffKernel + diffUser;
            if (totalSys <= 0) return 0.0;

            // diffKernel includes idle time
            var usage = (double)(totalSys - diffIdle) / totalSys * 100.0;
            return Math.Clamp(Math.Round(usage, 1), 0.0, 100.0);
        }
        catch
        {
            return 0.0;
        }
    }

    public static (string Name, string Build) GetOperatingSystemDetails()
    {
        string name = "Windows";
        string build = string.Empty;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                var buildObj = key.GetValue("CurrentBuild") ?? key.GetValue("CurrentBuildNumber");
                if (buildObj != null)
                {
                    build = buildObj.ToString() ?? string.Empty;
                }

                if (int.TryParse(build, out int buildInt))
                {
                    name = buildInt >= 22000 ? "Windows 11" : "Windows 10";
                }

                var displayVersion = key.GetValue("DisplayVersion")?.ToString();
                if (!string.IsNullOrEmpty(displayVersion))
                {
                    name += $" ({displayVersion})";
                }
            }
        }
        catch
        {
            name = Environment.OSVersion.Version.Major >= 10 ? "Windows 10/11" : "Windows";
            build = Environment.OSVersion.Version.Build.ToString();
        }

        return (name, $"Build {build}");
    }
}
