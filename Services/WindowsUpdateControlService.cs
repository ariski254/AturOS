using System;
using System.Threading.Tasks;
using AturOS.Helpers;
using Microsoft.Win32;

namespace AturOS.Services;

public class WindowsUpdateControlService
{
    public async Task<(bool Success, string Message)> ApplyHardLockdownAsync()
    {
        if (!AdministratorHelper.IsAdministrator) return (false, "Hard Lockdown membutuhkan hak Administrator.");

        LoggerService.Instance.Info("Menerapkan Hard Lockdown pada Windows Update...");

        // 1. Disable services
        var services = new[] { "wuauserv", "UsoSvc", "WaaSMedicSvc" };
        foreach (var s in services)
        {
            await ProcessHelper.RunCommandAsync("sc.exe", $"config {s} start=disabled");
            await ProcessHelper.RunCommandAsync("sc.exe", $"stop {s}");
        }

        // 2. Registry Policies: NoAutoUpdate
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU");
            key.SetValue("NoAutoUpdate", 1, RegistryValueKind.DWord);
            key.SetValue("AUOptions", 1, RegistryValueKind.DWord); // 1 = Keep my computer up to date is disabled
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Catatan penetapan registry AU Windows Update: {ex.Message}");
        }

        // 3. Disable Update Orchestrator scheduled tasks
        await ProcessHelper.RunPowerShellScriptAsync(@"
            Get-ScheduledTask -TaskPath '\Microsoft\Windows\UpdateOrchestrator\*' -ErrorAction SilentlyContinue | Disable-ScheduledTask -ErrorAction SilentlyContinue
            Get-ScheduledTask -TaskPath '\Microsoft\Windows\WindowsUpdate\*' -ErrorAction SilentlyContinue | Disable-ScheduledTask -ErrorAction SilentlyContinue
        ");

        LoggerService.Instance.Success("Hard Lockdown Windows Update berhasil diterapkan.");
        return (true, "Hard Lockdown aktif: Seluruh service update, scheduled task, dan auto-update telah dimatikan dan dikunci.");
    }

    public async Task<(bool Success, string Message)> ApplyPauseTo2099Async()
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");

            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings");
                string pauseDate = "2099-12-31T23:59:59Z";
                key.SetValue("PauseFeatureUpdatesStartTime", pauseDate, RegistryValueKind.String);
                key.SetValue("PauseFeatureUpdatesEndTime", pauseDate, RegistryValueKind.String);
                key.SetValue("PauseQualityUpdatesStartTime", pauseDate, RegistryValueKind.String);
                key.SetValue("PauseQualityUpdatesEndTime", pauseDate, RegistryValueKind.String);
                key.SetValue("PauseUpdatesStartTime", pauseDate, RegistryValueKind.String);
                key.SetValue("PauseUpdatesExpiryTime", pauseDate, RegistryValueKind.String);

                LoggerService.Instance.Success("Jeda Windows Update hingga tahun 2099 diterapkan.");
                return (true, "Pembaruan Windows dijeda hingga tahun 2099 tanpa merusak dependensi Microsoft Store / Xbox.");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Gagal menyetel jeda update: {ex.Message}");
                return (false, $"Gagal menyetel jeda update: {ex.Message}");
            }
        });
    }

    public async Task<(bool Success, string Message)> ApplySecurityOnlyModeAsync()
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");

            try
            {
                var (osName, osBuild) = SystemInfoService.GetOperatingSystemDetails();
                string targetVersion = osName.Contains("11") ? "Windows 11" : "Windows 10";

                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate");
                key.SetValue("TargetReleaseVersion", 1, RegistryValueKind.DWord);
                key.SetValue("ProductVersion", targetVersion, RegistryValueKind.String);

                using var subKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                var displayVer = subKey?.GetValue("DisplayVersion")?.ToString() ?? "23H2";
                key.SetValue("TargetReleaseVersionInfo", displayVer, RegistryValueKind.String);

                LoggerService.Instance.Success("Mode Hanya Patch Keamanan diterapkan.");
                return (true, $"Sistem dikunci pada build {displayVer} untuk hanya menerima pembaruan keamanan dan virus tanpa feature update besar.");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Gagal menerapkan Mode Keamanan: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public async Task<(bool Success, string Message)> ApplyManualNotificationModeAsync()
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");

            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU");
                key.SetValue("NoAutoUpdate", 0, RegistryValueKind.DWord);
                key.SetValue("AUOptions", 2, RegistryValueKind.DWord); // 2 = Notify for download and notify for install

                LoggerService.Instance.Success("Mode Notifikasi Manual diterapkan (AUOptions = 2).");
                return (true, "Mode Notifikasi Manual aktif: Windows tidak akan mengunduh update tanpa konfirmasi izin pengguna.");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Gagal menerapkan Mode Notifikasi: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public async Task<(bool Success, string Message)> BlockDriverUpdatesAsync(bool block)
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");

            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate");
                key.SetValue("ExcludeWUDriversInQualityUpdate", block ? 1 : 0, RegistryValueKind.DWord);

                LoggerService.Instance.Success($"Blokir driver otomatis: {(block ? "Aktif" : "Nonaktif")}.");
                return (true, block ? "Windows Update dilarang menimpa driver hardware yang sudah terpasang." : "Pembaruan driver otomatis dikembalikan ke standar.");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Gagal menyetel pembaruan driver: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public async Task<(bool Success, string Message)> RestoreDefaultUpdateAsync()
    {
        if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");

        LoggerService.Instance.Info("Mengembalikan Windows Update ke setelan pabrik...");

        // 1. Re-enable services
        await ProcessHelper.RunCommandAsync("sc.exe", "config wuauserv start=auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "start wuauserv");
        await ProcessHelper.RunCommandAsync("sc.exe", "config UsoSvc start=delayed-auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "start UsoSvc");
        await ProcessHelper.RunCommandAsync("sc.exe", "config WaaSMedicSvc start=demand");

        // 2. Remove policies
        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", false);

            using var pauseKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings", true);
            if (pauseKey != null)
            {
                pauseKey.DeleteValue("PauseFeatureUpdatesStartTime", false);
                pauseKey.DeleteValue("PauseFeatureUpdatesEndTime", false);
                pauseKey.DeleteValue("PauseQualityUpdatesStartTime", false);
                pauseKey.DeleteValue("PauseQualityUpdatesEndTime", false);
                pauseKey.DeleteValue("PauseUpdatesStartTime", false);
                pauseKey.DeleteValue("PauseUpdatesExpiryTime", false);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Catatan saat menghapus registry update: {ex.Message}");
        }

        // 3. Re-enable tasks
        await ProcessHelper.RunPowerShellScriptAsync(@"
            Get-ScheduledTask -TaskPath '\Microsoft\Windows\UpdateOrchestrator\*' -ErrorAction SilentlyContinue | Enable-ScheduledTask -ErrorAction SilentlyContinue
            Get-ScheduledTask -TaskPath '\Microsoft\Windows\WindowsUpdate\*' -ErrorAction SilentlyContinue | Enable-ScheduledTask -ErrorAction SilentlyContinue
        ");

        LoggerService.Instance.Success("Windows Update berhasil dikembalikan ke pengaturan default Windows.");
        return (true, "Konfigurasi Windows Update berhasil dikembalikan ke standar pabrik.");
    }
}
