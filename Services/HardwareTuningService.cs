using System.IO;
using AturOS.Helpers;
using AturOS.Models;
using Microsoft.Win32;

namespace AturOS.Services;

public class HardwareTuningService
{
    // ==========================================
    // 1. TWEAK RAM
    // ==========================================
    public bool GetDisablePagingExecutiveStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management");
            var val = key?.GetValue("DisablePagingExecutive");
            return val is int i && i == 1;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetDisablePagingExecutiveAsync(bool enable)
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
            bool ok = RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "DisablePagingExecutive", enable ? 1 : 0, backup: true);
            return (ok, ok ? (enable ? "Kernel Windows dipaksa tetap berada di RAM fisik." : "Pengaturan paging kernel dikembalikan ke default.") : "Gagal mengubah setting paging.");
        });
    }

    public List<StartupItem> GetStartupApps()
    {
        var list = new List<StartupItem>();

        // HKCU Run
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    list.Add(new StartupItem
                    {
                        Name = name,
                        Command = key.GetValue(name)?.ToString() ?? string.Empty,
                        Location = "HKCU\\Run",
                        IsEnabled = true
                    });
                }
            }
        }
        catch { }

        // HKLM Run
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            if (key != null)
            {
                foreach (var name in key.GetValueNames())
                {
                    list.Add(new StartupItem
                    {
                        Name = name,
                        Command = key.GetValue(name)?.ToString() ?? string.Empty,
                        Location = "HKLM\\Run",
                        IsEnabled = true
                    });
                }
            }
        }
        catch { }

        return list;
    }

    // ==========================================
    // 2. TWEAK CPU & LATENSI
    // ==========================================
    public async Task<(bool Success, string Message)> DisableCoreParkingAsync()
    {
        var res1 = await ProcessHelper.RunCommandAsync("powercfg.exe", "/setacvalueindex scheme_current sub_processor CPMINCORES 100");
        var res2 = await ProcessHelper.RunCommandAsync("powercfg.exe", "/setactive scheme_current");
        return (res1.Success && res2.Success, "Core Parking dinonaktifkan: Seluruh core CPU selalu aktif 100% tanpa parkir.");
    }

    public async Task<(bool Success, string Message)> DisableDynamicTickAsync()
    {
        if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
        var res = await ProcessHelper.RunCommandAsync("bcdedit.exe", "/set disabledynamictick yes");
        return (res.Success, res.Success ? "Dynamic Tick dinonaktifkan untuk menjaga kestabilan timer sistem." : $"Gagal: {res.StandardError}");
    }

    public async Task<(bool Success, string Message)> DisablePowerThrottlingAsync(bool disable)
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
            bool ok = RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", disable ? 1 : 0, backup: true);
            return (ok, ok ? (disable ? "Power Throttling latar depan dinonaktifkan." : "Power Throttling dikembalikan ke default.") : "Gagal mengubah setting power throttling.");
        });
    }

    // ==========================================
    // 3. TWEAK GPU & SHADER CACHE
    // ==========================================
    public async Task<(long BytesFreed, int FilesDeleted)> CleanShaderCacheAsync()
    {
        return await Task.Run(() =>
        {
            long freed = 0;
            int count = 0;

            var paths = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "DXCache"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "GLCache"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMD", "DxCache"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Intel", "ShaderCache")
            };

            foreach (var p in paths)
            {
                if (!Directory.Exists(p)) continue;
                try
                {
                    foreach (var f in Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fi = new FileInfo(f);
                            long len = fi.Length;
                            fi.Attributes = FileAttributes.Normal;
                            fi.Delete();
                            freed += len;
                            count++;
                        }
                        catch { }
                    }
                }
                catch { }
            }

            LoggerService.Instance.Success($"Pembersihan Shader Cache selesai: {count} file dihapus.");
            return (freed, count);
        });
    }

    public bool GetHagsStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
            var val = key?.GetValue("HwSchMode");
            return val is int i && i == 2;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetHagsAsync(bool enable)
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Mengubah HAGS membutuhkan hak Administrator.");
            bool ok = RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", enable ? 2 : 1, backup: true);
            return (ok, ok ? $"Hardware-Accelerated GPU Scheduling disetel ke {(enable ? "ON" : "OFF")}. Perlu restart PC." : "Gagal mengubah status HAGS.");
        });
    }

    public async Task<(bool Success, string Message)> DisableGameDvrAsync(bool disable)
    {
        return await Task.Run(() =>
        {
            bool ok1 = RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", disable ? 0 : 1, backup: true);
            bool ok2 = RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", disable ? 0 : 1, backup: true);
            return (ok1 || ok2, disable ? "Xbox Game DVR & perekaman background dinonaktifkan." : "Game DVR dikembalikan ke default.");
        });
    }

    public async Task<(bool Success, string Message)> OptimizeTdrDelayAsync()
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
            bool ok1 = RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "TdrDelay", 10, backup: true);
            bool ok2 = RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "TdrDdiDelay", 10, backup: true);
            return (ok1 && ok2, "Nilai TdrDelay GPU berhasil dioptimalkan ke 10 detik untuk mencegah crash timeout.");
        });
    }

    // ==========================================
    // 4. TWEAK DISK & JARINGAN
    // ==========================================
    public async Task<(bool Success, string Message)> DisableNtfs8Dot3Async()
    {
        if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
        var res = await ProcessHelper.RunCommandAsync("fsutil.exe", "8dot3name set 1");
        return (res.Success, res.Success ? "NTFS 8.3 Name Creation dinonaktifkan untuk mempercepat I/O disk." : $"Gagal: {res.StandardError}");
    }

    public async Task<(bool Success, string Message)> EnableSsdTrimAsync()
    {
        if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
        var res = await ProcessHelper.RunCommandAsync("fsutil.exe", "behavior set DisableDeleteNotify 0");
        return (res.Success, res.Success ? "Fungsi TRIM pada SSD aktif dan terverifikasi." : $"Gagal: {res.StandardError}");
    }

    public async Task<(bool Success, string Message)> DisableNetworkThrottlingAsync()
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
            bool ok1 = RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xffffffff), backup: true);
            bool ok2 = RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0, backup: true);
            return (ok1 && ok2, "Network Throttling Index dinonaktifkan (limitasi bandwidth dihapus).");
        });
    }

    public async Task<(bool Success, string Message)> DisableNagleAlgorithmAsync()
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
            try
            {
                using var intKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces");
                if (intKey != null)
                {
                    foreach (var sub in intKey.GetSubKeyNames())
                    {
                        try
                        {
                            using var adapterKey = intKey.OpenSubKey(sub, true);
                            adapterKey?.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                            adapterKey?.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                        }
                        catch { }
                    }
                }
                return (true, "Algoritma Nagle (TCP No Delay) dinonaktifkan pada seluruh adapter untuk memangkas ping game.");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public async Task<(bool Success, string Message)> FlushDnsAsync()
    {
        var res = await ProcessHelper.RunCommandAsync("ipconfig.exe", "/flushdns");
        return (res.Success, res.Success ? "Cache resolver DNS sistem berhasil dibersihkan (Flush DNS)." : $"Gagal: {res.StandardError}");
    }

    // ==========================================
    // 5. TWEAK INPUT & RESPONSIFITAS
    // ==========================================
    public async Task<(bool Success, string Message)> DisableMouseAccelerationAsync()
    {
        return await Task.Run(() =>
        {
            bool ok1 = RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "0", backup: true);
            bool ok2 = RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "0", backup: true);
            bool ok3 = RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "0", backup: true);
            return (ok1 && ok2 && ok3, "Akselerasi mouse Windows dinonaktifkan (akurasi 1:1 murni).");
        });
    }

    public async Task<(bool Success, string Message)> MaximizeKeyboardResponseAsync()
    {
        return await Task.Run(() =>
        {
            bool ok1 = RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Keyboard", "KeyboardDelay", "0", backup: true);
            bool ok2 = RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Keyboard", "KeyboardSpeed", "31", backup: true);
            return (ok1 && ok2, "Kecepatan repeat rate keyboard disetel ke respon maksimum.");
        });
    }

    public async Task<(bool Success, string Message)> DisableStickyKeysPopupAsync()
    {
        return await Task.Run(() =>
        {
            bool ok = RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Accessibility\StickyKeys", "Flags", "506", backup: true);
            return (ok, "Pop-up Sticky Keys dinonaktifkan.");
        });
    }

    // ==========================================
    // 6. TWEAK PERFORMA TAMBAHAN (POWER PLAN, JADWAL CPU/GPU, RESPONSIFITAS UI)
    // ==========================================
    private readonly PowerPlanService _powerPlanService = new();

    public async Task<bool> IsUltimatePerformanceActiveAsync()
    {
        var tweak = _powerPlanService.GetPowerPlanTweakItem();
        await _powerPlanService.RefreshPowerPlanStatusAsync(tweak);
        return tweak.Status == TweakStatus.Aktif;
    }

    public async Task<(bool Success, string Message)> SetUltimatePerformanceAsync(bool enable)
    {
        var tweak = _powerPlanService.GetPowerPlanTweakItem();
        if (enable)
        {
            return await _powerPlanService.EnableUltimatePerformanceAsync(tweak);
        }
        else
        {
            return await _powerPlanService.RestoreBalancedPlanAsync(tweak);
        }
    }

    public bool GetWin32PrioritySeparationStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl");
            var val = key?.GetValue("Win32PrioritySeparation");
            return val is int i && i == 38;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetWin32PrioritySeparationAsync(bool optimize)
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
            int val = optimize ? 38 : 2;
            bool ok = RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", val, backup: true);
            return (ok, ok ? (optimize ? "Prioritas CPU latar depan disetel ke maksimum (Win32PrioritySeparation = 38)." : "Penjadwalan prioritas CPU dikembalikan ke default Windows (2).") : "Gagal mengubah Win32PrioritySeparation.");
        });
    }

    public bool GetMenuShowDelayStatus()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var val = key?.GetValue("MenuShowDelay")?.ToString();
            return val == "0";
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetMenuShowDelayAsync(bool instant)
    {
        return await Task.Run(() =>
        {
            string val = instant ? "0" : "400";
            bool ok = RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay", val, backup: true);
            return (ok, ok ? (instant ? "Delay menu disetel ke 0 ms (Instan tanpa jeda rendering)." : "Delay menu dikembalikan ke default (400 ms).") : "Gagal mengubah MenuShowDelay.");
        });
    }

    public bool GetNetworkThrottlingStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile");
            var val = key?.GetValue("NetworkThrottlingIndex");
            return val is int i && (i == unchecked((int)0xffffffff) || i == -1);
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetNetworkThrottlingAsync(bool disable)
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
            int throttleVal = disable ? unchecked((int)0xffffffff) : 10;
            int respVal = disable ? 0 : 20;
            bool ok1 = RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", throttleVal, backup: true);
            bool ok2 = RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", respVal, backup: true);
            return (ok1 && ok2, disable ? "Network Throttling dinonaktifkan (limitasi bandwidth jaringan dihapus)." : "Network Throttling dikembalikan ke default.");
        });
    }

    public bool GetGpuPrioritySchedulingStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games");
            var gpuPri = key?.GetValue("GPU Priority");
            var pri = key?.GetValue("Priority");
            return gpuPri is int g && g == 8 && pri is int p && p == 6;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetGpuPrioritySchedulingAsync(bool enable)
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
            string path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
            if (enable)
            {
                bool ok1 = RegistryHelper.SetDWord(RegistryHive.LocalMachine, path, "GPU Priority", 8, backup: true);
                bool ok2 = RegistryHelper.SetDWord(RegistryHive.LocalMachine, path, "Priority", 6, backup: true);
                bool ok3 = RegistryHelper.SetString(RegistryHive.LocalMachine, path, "Scheduling Category", "High", backup: true);
                bool ok4 = RegistryHelper.SetString(RegistryHive.LocalMachine, path, "SFIO Priority", "High", backup: true);
                return (ok1 && ok2 && ok3 && ok4, "Prioritas GPU & game multimedia disetel ke level maksimum.");
            }
            else
            {
                bool ok1 = RegistryHelper.SetDWord(RegistryHive.LocalMachine, path, "GPU Priority", 8, backup: true);
                bool ok2 = RegistryHelper.SetDWord(RegistryHive.LocalMachine, path, "Priority", 2, backup: true);
                bool ok3 = RegistryHelper.SetString(RegistryHive.LocalMachine, path, "Scheduling Category", "Medium", backup: true);
                bool ok4 = RegistryHelper.SetString(RegistryHive.LocalMachine, path, "SFIO Priority", "Normal", backup: true);
                return (ok1 && ok2 && ok3 && ok4, "Penjadwalan prioritas GPU dikembalikan ke default Windows.");
            }
        });
    }
}
