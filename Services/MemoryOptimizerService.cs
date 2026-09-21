using System.Diagnostics;
using AturOS.Helpers;
using AturOS.Models;

namespace AturOS.Services;

public class MemoryOptimizationResult
{
    public int ProcessedCount { get; set; }
    public int SkippedCount { get; set; }
    public double RamBeforeGb { get; set; }
    public double RamAfterGb { get; set; }
    public double RamFreedMb => Math.Max(0, (RamBeforeGb - RamAfterGb) * 1024.0);
    public string FormattedFreed => $"{RamFreedMb:F0} MB";
}

public class MemoryOptimizerService
{
    public async Task<List<TopProcessItem>> GetTopProcessesAsync(int count = 10)
    {
        return await Task.Run(() =>
        {
            var list = new List<TopProcessItem>();
            try
            {
                var processes = Process.GetProcesses();
                foreach (var p in processes)
                {
                    try
                    {
                        if (p.Id <= 4) continue; // Skip System Idle and System
                        list.Add(new TopProcessItem
                        {
                            Id = p.Id,
                            ProcessName = p.ProcessName,
                            WorkingSetBytes = p.WorkingSet64
                        });
                    }
                    catch { }
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Warning($"Gagal enumerasi proses: {ex.Message}");
            }

            return list.OrderByDescending(p => p.WorkingSetBytes).Take(count).ToList();
        });
    }

    public async Task<MemoryOptimizationResult> OptimizeRamAsync(Action<string>? progressCallback = null)
    {
        return await Task.Run(() =>
        {
            var result = new MemoryOptimizationResult();

            // 1. Measure RAM before
            var memBefore = new WindowsApi.MEMORYSTATUSEX();
            if (WindowsApi.GlobalMemoryStatusEx(memBefore))
            {
                result.RamBeforeGb = Math.Round((double)(memBefore.ullTotalPhys - memBefore.ullAvailPhys) / (1024 * 1024 * 1024), 2);
            }

            LoggerService.Instance.Info("Memulai optimasi working set memori...");
            progressCallback?.Invoke("Mengambil daftar proses sistem...");

            var currentPid = Environment.ProcessId;
            var processes = Process.GetProcesses();

            foreach (var p in processes)
            {
                try
                {
                    // Skip system processes and self
                    if (p.Id <= 4 || p.Id == currentPid)
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    // Open process with minimal required permissions: PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED_INFORMATION
                    const uint desiredAccess = WindowsApi.PROCESS_SET_QUOTA | WindowsApi.PROCESS_QUERY_LIMITED_INFORMATION;
                    IntPtr hProcess = WindowsApi.OpenProcess(desiredAccess, false, p.Id);

                    if (hProcess != IntPtr.Zero)
                    {
                        try
                        {
                            int res = WindowsApi.EmptyWorkingSet(hProcess);
                            if (res != 0)
                            {
                                result.ProcessedCount++;
                            }
                            else
                            {
                                result.SkippedCount++;
                            }
                        }
                        finally
                        {
                            WindowsApi.CloseHandle(hProcess);
                        }
                    }
                    else
                    {
                        result.SkippedCount++;
                    }
                }
                catch (Exception)
                {
                    result.SkippedCount++;
                }
                finally
                {
                    p.Dispose();
                }
            }

            // 2. Measure RAM after
            Thread.Sleep(300); // Allow OS memory manager to update counters
            var memAfter = new WindowsApi.MEMORYSTATUSEX();
            if (WindowsApi.GlobalMemoryStatusEx(memAfter))
            {
                result.RamAfterGb = Math.Round((double)(memAfter.ullTotalPhys - memAfter.ullAvailPhys) / (1024 * 1024 * 1024), 2);
            }

            LoggerService.Instance.Success($"Optimasi RAM selesai: {result.ProcessedCount} proses di-trim, {result.SkippedCount} dilewati. Estimasi dibebaskan: {result.FormattedFreed}.");
            return result;
        });
    }

    // ==========================================
    // STANDBY LIST & CACHE FLUSH
    // ==========================================
    public async Task<MemoryOptimizationResult> FlushStandbyListAsync()
    {
        return await Task.Run(async () =>
        {
            var result = new MemoryOptimizationResult();
            var memBefore = new WindowsApi.MEMORYSTATUSEX();
            if (WindowsApi.GlobalMemoryStatusEx(memBefore))
            {
                result.RamBeforeGb = Math.Round((double)(memBefore.ullTotalPhys - memBefore.ullAvailPhys) / (1024 * 1024 * 1024), 2);
            }

            LoggerService.Instance.Info("Membersihkan Standby List dan System Cache...");

            GC.Collect();
            GC.WaitForPendingFinalizers();

            try
            {
                var hCurrent = Process.GetCurrentProcess().Handle;
                WindowsApi.SetProcessWorkingSetSize(hCurrent, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }

            await OptimizeRamAsync();

            Thread.Sleep(300);
            var memAfter = new WindowsApi.MEMORYSTATUSEX();
            if (WindowsApi.GlobalMemoryStatusEx(memAfter))
            {
                result.RamAfterGb = Math.Round((double)(memAfter.ullTotalPhys - memAfter.ullAvailPhys) / (1024 * 1024 * 1024), 2);
            }

            LoggerService.Instance.Success($"Standby List dibersihkan. Memori dibebaskan: {result.FormattedFreed}.");
            return result;
        });
    }

    // ==========================================
    // TWEAK RAM SISTEM
    // ==========================================
    public bool GetDisablePagingExecutiveStatus()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management");
            var val = key?.GetValue("DisablePagingExecutive");
            return val is int i && i == 1;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal membaca status DisablePagingExecutive: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool Success, string Message)> SetDisablePagingExecutiveAsync(bool enable)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
                bool ok = RegistryHelper.SetDWord(Microsoft.Win32.RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "DisablePagingExecutive", enable ? 1 : 0, backup: true);
                return (ok, ok ? (enable ? "Kernel Windows dipaksa tetap berada di RAM fisik murni." : "Pengaturan paging kernel dikembalikan ke default.") : "Gagal mengubah setting DisablePagingExecutive.");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Error setting DisablePagingExecutive: {ex.Message}");
                return (false, $"Terjadi kesalahan: {ex.Message}");
            }
        });
    }

    public bool GetClearPageFileAtShutdownStatus()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management");
            var val = key?.GetValue("ClearPageFileAtShutdown");
            return val is int i && i == 1;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal membaca status ClearPageFileAtShutdown: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool Success, string Message)> SetClearPageFileAtShutdownAsync(bool enable)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
                bool ok = RegistryHelper.SetDWord(Microsoft.Win32.RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "ClearPageFileAtShutdown", enable ? 1 : 0, backup: true);
                return (ok, ok ? (enable ? "Pembersihan pagefile saat shutdown diaktifkan." : "Pembersihan pagefile saat shutdown dinonaktifkan.") : "Gagal mengubah setting ClearPageFileAtShutdown.");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Error setting ClearPageFileAtShutdown: {ex.Message}");
                return (false, $"Terjadi kesalahan: {ex.Message}");
            }
        });
    }

    public bool GetLargeSystemCacheStatus()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management");
            var val = key?.GetValue("LargeSystemCache");
            return val is int i && i == 1;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal membaca status LargeSystemCache: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool Success, string Message)> SetLargeSystemCacheAsync(bool enable)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
                bool ok = RegistryHelper.SetDWord(Microsoft.Win32.RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "LargeSystemCache", enable ? 1 : 0, backup: true);
                return (ok, ok ? (enable ? "Ukuran Large System Cache diaktifkan untuk throughput file I/O memori." : "Ukuran cache sistem dikembalikan ke default aplikasi.") : "Gagal mengubah setting LargeSystemCache.");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Error setting LargeSystemCache: {ex.Message}");
                return (false, $"Terjadi kesalahan: {ex.Message}");
            }
        });
    }

    public async Task<bool?> GetMemoryCompressionStatusAsync()
    {
        try
        {
            var res = await ProcessHelper.RunPowerShellScriptAsync("(Get-MMAgent).MemoryCompression", timeoutMs: 15000);
            if (res.Success && !string.IsNullOrWhiteSpace(res.StandardOutput))
            {
                if (bool.TryParse(res.StandardOutput.Trim(), out bool val))
                {
                    return val;
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal membaca status MemoryCompression: {ex.Message}");
        }
        return null;
    }

    public async Task<(bool Success, string Message)> SetMemoryCompressionAsync(bool enable)
    {
        if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");

        try
        {
            string cmd = enable ? "Enable-MMAgent -mc" : "Disable-MMAgent -mc";
            var res = await ProcessHelper.RunPowerShellScriptAsync(cmd, timeoutMs: 20000);
            if (res.Success)
            {
                return (true, enable ? "Kompresi Memori Windows (Memory Compression) diaktifkan." : "Kompresi Memori Windows dimatikan (menghemat beban siklus CPU).");
            }
            return (false, $"Gagal mengubah status kompresi memori: {res.StandardError}");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Error setting MemoryCompression: {ex.Message}");
            return (false, $"Terjadi kesalahan: {ex.Message}");
        }
    }

    public bool GetSysMainStatus()
    {
        try
        {
            using var sc = new System.ServiceProcess.ServiceController("SysMain");
            return sc.Status == System.ServiceProcess.ServiceControllerStatus.Running;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Layanan SysMain tidak dapat diakses atau tidak ditemukan: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool Success, string Message)> SetSysMainStatusAsync(bool enable)
    {
        if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");

        try
        {
            if (enable)
            {
                await ProcessHelper.RunCommandAsync("sc.exe", "config SysMain start=auto");
                var res = await ProcessHelper.RunCommandAsync("sc.exe", "start SysMain");
                return (res.Success, "Layanan SysMain (Superfetch) berhasil diaktifkan.");
            }
            else
            {
                await ProcessHelper.RunCommandAsync("sc.exe", "config SysMain start=disabled");
                var res = await ProcessHelper.RunCommandAsync("sc.exe", "stop SysMain");
                return (res.Success, "Layanan SysMain (Superfetch) berhasil dinonaktifkan.");
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Error setting SysMain status: {ex.Message}");
            return (false, $"Terjadi kesalahan pada layanan SysMain: {ex.Message}");
        }
    }
}
