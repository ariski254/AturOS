using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AturOS.Helpers;

namespace AturOS.Services;

public enum LidCloseAction
{
    DoNothing = 0,
    Sleep = 1,
    Hibernate = 2,
    Shutdown = 3
}

public class EmergencyToolsService
{
    public async Task<(bool Success, string Message)> KillNotRespondingTasksAsync()
    {
        LoggerService.Instance.Info("Menutup aplikasi yang tidak merespon (hang/freeze)...");
        var result = await ProcessHelper.RunCommandAsync("taskkill.exe", "/F /FI \"STATUS eq NOT RESPONDING\"");

        if (result.Success)
        {
            LoggerService.Instance.Success("Pembersihan aplikasi tidak merespon selesai.");
            return (true, "Aplikasi yang membeku/hang berhasil ditutup secara paksa.");
        }

        return (true, "Tidak ada proses yang sedang dalam status Not Responding saat ini.");
    }

    public async Task<bool> RestartExplorerAsync()
    {
        return await RegistryTweakService.RestartExplorerAsync();
    }

    public async Task<bool> ClearClipboardAsync()
    {
        try
        {
            if (System.Windows.Application.Current != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Clipboard.Clear();
                });
            }
            await ProcessHelper.RunCommandAsync("cmd.exe", "/c echo off | clip");
            LoggerService.Instance.Success("Riwayat clipboard sistem dibersihkan.");
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal membersihkan clipboard: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool Success, string PathOrMessage)> GenerateBatteryReportAsync()
    {
        var outPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "battery-report.html");
        LoggerService.Instance.Info($"Membuat Battery Report di {outPath}...");

        var result = await ProcessHelper.RunCommandAsync("powercfg.exe", $"/batteryreport /output \"{outPath}\"");

        if (result.Success && File.Exists(outPath))
        {
            LoggerService.Instance.Success("Laporan baterai berhasil dibuat.");
            try
            {
                Process.Start(new ProcessStartInfo { FileName = outPath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Warning($"Gagal membuka browser default untuk laporan baterai: {ex.Message}");
            }
            return (true, outPath);
        }

        return (false, "Sistem ini kemungkinan adalah PC Desktop (tanpa baterai laptop) atau akses ditolak.");
    }

    public async Task<(bool Success, string Message)> SetLidCloseActionAsync(LidCloseAction action)
    {
        int val = (int)action;
        var res1 = await ProcessHelper.RunCommandAsync("powercfg.exe", $"/setacvalueindex scheme_current sub_buttons lidaction {val}");
        var res2 = await ProcessHelper.RunCommandAsync("powercfg.exe", $"/setdcvalueindex scheme_current sub_buttons lidaction {val}");
        await ProcessHelper.RunCommandAsync("powercfg.exe", "/setactive scheme_current");

        string desc = action switch
        {
            LidCloseAction.DoNothing => "Tidak Melakukan Apa-apa (Do Nothing)",
            LidCloseAction.Sleep => "Tidur (Sleep)",
            LidCloseAction.Hibernate => "Hibernasi",
            LidCloseAction.Shutdown => "Matikan Komputer (Shutdown)",
            _ => "Standar"
        };

        return (res1.Success && res2.Success, $"Aksi tutup layar laptop disetel ke: {desc}.");
    }

    public async Task<(bool Success, string Message)> SetSafeModeBootAsync(bool enableSafeMode)
    {
        if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");

        if (enableSafeMode)
        {
            var res = await ProcessHelper.RunCommandAsync("bcdedit.exe", "/set {current} safeboot minimal");
            return (res.Success, res.Success ? "PC disetel untuk masuk ke Safe Mode pada restart berikutnya." : $"Gagal: {res.StandardError}");
        }
        else
        {
            var res = await ProcessHelper.RunCommandAsync("bcdedit.exe", "/deletevalue {current} safeboot");
            return (res.Success, res.Success ? "Pengaturan Safe Mode dihapus. PC akan boot normal." : $"Gagal: {res.StandardError}");
        }
    }

    public async Task<(bool Success, string Message)> RunSfcScannowAsync(Action<string>? onProgress = null)
    {
        if (!AdministratorHelper.IsAdministrator) return (false, "Pemeriksaan SFC memerlukan hak Administrator.");

        LoggerService.Instance.Info("Menjalankan sfc /scannow...");
        onProgress?.Invoke("Menjalankan pemeriksaan integritas file sistem (sfc /scannow)...");

        var result = await ProcessHelper.RunCommandAsync("sfc.exe", "/scannow", timeoutMs: 900000); // 15 min
        return (result.Success, result.Success ? "Pemeriksaan SFC selesai. File sistem Windows telah diverifikasi." : $"Hasil SFC: {result.StandardOutput}");
    }

    public async Task<(bool Success, string Message)> RunDismRestoreHealthAsync(Action<string>? onProgress = null)
    {
        if (!AdministratorHelper.IsAdministrator) return (false, "Pemulihan DISM memerlukan hak Administrator.");

        LoggerService.Instance.Info("Menjalankan DISM /RestoreHealth...");
        onProgress?.Invoke("Memulihkan citra sistem Windows (DISM /Online /Cleanup-Image /RestoreHealth)...");

        var result = await ProcessHelper.RunCommandAsync("dism.exe", "/online /cleanup-image /restorehealth", timeoutMs: 900000); // 15 min
        return (result.Success, result.Success ? "Pemulihan citra sistem DISM berhasil diselesaikan." : $"DISM gagal: {result.StandardError}");
    }

    public async Task<int> GetGhostDeviceCountAsync()
    {
        string psScript = @"
$classes = @('USB', 'DiskDrive', 'Ports', 'Bluetooth', 'WPD', 'Mouse', 'Keyboard', 'MEDIA', 'HIDClass', 'Net')
$count = (Get-PnpDevice | Where-Object { -not $_.Present -and $classes -contains $_.Class }).Count
Write-Output ""GHOST_COUNT:$count""
";
        var result = await ProcessHelper.RunPowerShellScriptAsync(psScript, timeoutMs: 30000);
        if (result.Success)
        {
            var match = System.Text.RegularExpressions.Regex.Match(result.StandardOutput, @"GHOST_COUNT:(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
            {
                return count;
            }
        }
        return 0;
    }

    public async Task<(bool Success, string Message, int CleanedCount)> CleanGhostDevicesAsync(Action<string>? onProgress = null)
    {
        if (!AdministratorHelper.IsAdministrator)
            return (false, "Membersihkan Ghost Devices memerlukan hak Administrator.", 0);

        LoggerService.Instance.Info("Memulai pemindaian dan pembersihan ghost devices (driver perangkat terputus)...");
        onProgress?.Invoke("Memindai dan membersihkan perangkat terputus (phantom/ghost devices)...");

        string psScript = @"
$classes = @('USB', 'DiskDrive', 'Ports', 'Bluetooth', 'WPD', 'Mouse', 'Keyboard', 'MEDIA', 'HIDClass', 'Net')
$devices = Get-PnpDevice | Where-Object { -not $_.Present -and $classes -contains $_.Class }
$count = 0
foreach ($d in $devices) {
    if ($d.InstanceId) {
        & pnputil.exe /remove-device ""$($d.InstanceId)"" | Out-Null
        $count++
    }
}
Write-Output ""CLEANED_COUNT:$count""
";
        var result = await ProcessHelper.RunPowerShellScriptAsync(psScript, timeoutMs: 120000);
        if (!result.Success && string.IsNullOrEmpty(result.StandardOutput))
        {
            LoggerService.Instance.Error($"Gagal membersihkan ghost devices: {result.StandardError}");
            return (false, $"Gagal membersihkan ghost devices: {result.StandardError}", 0);
        }

        int cleanedCount = 0;
        var match = System.Text.RegularExpressions.Regex.Match(result.StandardOutput, @"CLEANED_COUNT:(\d+)");
        if (match.Success)
        {
            int.TryParse(match.Groups[1].Value, out cleanedCount);
        }

        LoggerService.Instance.Success($"Pembersihan ghost devices selesai. {cleanedCount} perangkat lama/hantu dibersihkan.");
        return (true, $"Berhasil membersihkan {cleanedCount} driver perangkat terputus (ghost devices).", cleanedCount);
    }
}
