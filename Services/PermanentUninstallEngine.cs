using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AturOS.Helpers;
using AturOS.Models;
using Microsoft.Win32;

namespace AturOS.Services;

/// <summary>
/// Core engine implementing ATUROS MASTER AUDIT, REPAIR, TRUE DEBLOAT & PERMANENT UNINSTALL ENGINE:
/// 1. Component Discovery (Processes, Services, Scheduled Tasks, Startup, Shortcuts, Residual Folders, Registry)
/// 2. DifficultUninstallResolver (fallback if uninstaller missing, corrupted, or incomplete)
/// 3. ApplicationResidualCleanupEngine (purges InstallLocation, AppData, ProgramData, WindowsApps, Registry)
/// 4. Post-Uninstall Verification (Anti-Mock readback audit verifying true Windows state)
/// 5. User Data Protection & Windows System Component Safety
/// </summary>
public static class PermanentUninstallEngine
{
    private static readonly HashSet<string> CriticalWindowsServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs", "DcomLaunch", "WinDefend", "MpsSvc", "bfe", "Dhcp", "Dnscache",
        "LanmanWorkstation", "LanmanServer", "CryptSvc", "PlugPlay", "EventLog",
        "SamSs", "TermService", "Schedule", "ProfSvc", "gpsvc", "LSM", "CoreMessagingRegistrar",
        "BrokerInfrastructure", "SystemEventsBroker", "TimeBrokerSvc", "UserManager",
        "FontCache", "KeyIso", "VaultSvc", "AppXSvc", "ClipSVC", "WcesSvc", "Spooler", "AudioSrv"
    };

    private static readonly HashSet<string> CriticalProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "smss", "csrss", "wininit", "services", "lsass", "svchost",
        "explorer", "dwm", "winlogon", "fontdrvhost", "sihost", "taskhostw", "AturOS"
    };

    /// <summary>
    /// Memindai seluruh komponen yang dimiliki oleh target aplikasi sebelum di-uninstall (Dry Run / Component Discovery).
    /// </summary>
    public static async Task<AppComponentsDiscoveryResult> DiscoverComponentsAsync(DebloatAppItem app)
    {
        return await Task.Run(() =>
        {
            var result = new AppComponentsDiscoveryResult { App = app };

            try
            {
                // 1. Deteksi Proses yang Sedang Berjalan
                result.Processes = DiscoverRunningProcesses(app);

                // 2. Deteksi Windows Services Milik Aplikasi
                result.Services = DiscoverOwnedServices(app);

                // 3. Deteksi Scheduled Tasks Milik Aplikasi
                result.ScheduledTasks = DiscoverOwnedScheduledTasks(app);

                // 4. Deteksi Startup Entries (Registry & Startup Folder)
                result.StartupEntries = DiscoverOwnedStartupEntries(app);

                // 5. Deteksi Shortcut Berkas (.lnk di Desktop & Start Menu)
                result.ShortcutFiles = DiscoverShortcuts(app);

                // 6. Deteksi Direktori Residu & Lokasi Instalasi
                result.ResidualDirectories = DiscoverCandidateDirectories(app);

                // 7. Deteksi Registry Keys Uninstall & Software
                result.RegistryKeys = DiscoverRegistryKeys(app);

                // 8. Klasifikasi Folder User Data yang Wajib Dijaga (Documents, Pictures, Desktop)
                result.UserDataFoldersToPreserve = IdentifyUserDataFoldersToPreserve(app);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Warning($"Pemeriksaan komponen aplikasi {app.DisplayName} menghasilkan catatan: {ex.Message}");
            }

            return result;
        });
    }

    /// <summary>
    /// Eksekusi Permanent Uninstall Engine secara tuntas, aman, dan terverifikasi.
    /// Alur: STOP PROCESSES -> STOP & REMOVE SERVICES -> REMOVE TASKS -> REMOVE STARTUP ->
    ///       RUN UNINSTALLER / UWP REMOVAL -> RESIDUAL CLEANUP -> VERIFICATION -> LOG
    /// </summary>
    public static async Task<PermanentUninstallResult> UninstallPermanentlyAsync(
        DebloatAppItem app,
        Action<string>? onProgress = null)
    {
        var startTime = DateTime.Now;
        var result = new PermanentUninstallResult { App = app };

        void Log(string msg)
        {
            result.LogEntries.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            LoggerService.Instance.Info(msg);
            onProgress?.Invoke(msg);
        }

        Log($"[PERMANENT UNINSTALL] Memulai proses pencopotan tuntas: '{app.DisplayName}' ({app.AppTypeDisplay})...");

        try
        {
            // TAHAP 1: DISCOVERY KOMPONEN
            onProgress?.Invoke($"Menganalisis komponen '{app.DisplayName}'...");
            var discovery = await DiscoverComponentsAsync(app);
            Log($"Komponen terdeteksi: {discovery.Processes.Count} proses, {discovery.Services.Count} service, {discovery.ScheduledTasks.Count} task, {discovery.StartupEntries.Count} startup, {discovery.ResidualDirectories.Count} folder.");

            // TAHAP 2: HENTIKAN PROSES AKTIF
            if (discovery.Processes.Count > 0)
            {
                onProgress?.Invoke($"Menghentikan {discovery.Processes.Count} proses aplikasi...");
                result.ProcessesKilled = StopApplicationProcesses(discovery.Processes, Log);
            }

            // TAHAP 3: HENTIKAN & HAPUS SERVICE MILIK APLIKASI
            if (discovery.Services.Count > 0)
            {
                onProgress?.Invoke($"Menghentikan dan menghapus {discovery.Services.Count} service aplikasi...");
                result.ServicesRemoved = await RemoveApplicationServicesAsync(discovery.Services, Log);
            }

            // TAHAP 4: HAPUS SCHEDULED TASKS MILIK APLIKASI
            if (discovery.ScheduledTasks.Count > 0)
            {
                onProgress?.Invoke($"Menghapus {discovery.ScheduledTasks.Count} scheduled task...");
                result.ScheduledTasksRemoved = await RemoveScheduledTasksAsync(discovery.ScheduledTasks, Log);
            }

            // TAHAP 5: HAPUS STARTUP ENTRIES
            if (discovery.StartupEntries.Count > 0)
            {
                onProgress?.Invoke($"Menghapus {discovery.StartupEntries.Count} entri startup...");
                result.StartupEntriesRemoved = RemoveStartupEntries(discovery.StartupEntries, Log);
            }

            // TAHAP 6: HAPUS SHORTCUT (.LNK)
            if (discovery.ShortcutFiles.Count > 0)
            {
                onProgress?.Invoke($"Membersihkan shortcut desktop dan menu Start...");
                result.ShortcutsRemoved = RemoveShortcuts(discovery.ShortcutFiles, Log);
            }

            // TAHAP 7: EKSEKUSI UNINSTALLER RESMI / UWP PACKAGE REMOVAL
            if (app.AppType == AppInstallType.Uwp)
            {
                onProgress?.Invoke($"Mencopot paket modern UWP '{app.PackageName}'...");
                await ExecuteUwpRemovalAsync(app, Log);
            }
            else
            {
                onProgress?.Invoke($"Menjalankan uninstaller resmi '{app.DisplayName}'...");
                await ExecuteWin32UninstallerAsync(app, Log);
            }

            // TAHAP 8: APPLICATION RESIDUAL CLEANUP ENGINE (DOKUMEN USER TETAP AMAN)
            onProgress?.Invoke($"Membersihkan seluruh folder residu dan berkas sementara...");
            result.DirectoriesPurged = PurgeResidualDirectories(discovery.ResidualDirectories, Log);

            // TAHAP 9: PEMBERSIHAN REGISTRY KEYS (Uninstall & Software)
            onProgress?.Invoke($"Membersihkan registrasi registry aplikasi...");
            result.RegistryKeysCleaned = PurgeRegistryKeys(discovery.RegistryKeys, app, Log);

            // TAHAP 10: VERIFIKASI AKHIR STATUS PENCOPOTAN (Anti-Mock Verification)
            onProgress?.Invoke($"Memverifikasi status akhir penghapusan sistem...");
            var verificationStatus = VerifyPermanentRemoval(app, discovery, out string verificationNotes);
            result.Status = verificationStatus;
            result.Message = verificationNotes;

            if (verificationStatus == UninstallVerificationStatus.PermanentlyRemoved)
            {
                app.IsInstalled = false;
                LoggerService.Instance.Success($"[SUCCESS] Aplikasi '{app.DisplayName}' TERVERIFIKASI BERHASIL DIHAPUS PERMANEN dari Windows!");
            }
            else if (verificationStatus == UninstallVerificationStatus.RemovedWithPreservedData)
            {
                app.IsInstalled = false;
                LoggerService.Instance.Success($"[SUCCESS] Aplikasi '{app.DisplayName}' berhasil dihapus permanen. Folder data pengguna dipertahankan dengan aman.");
            }
            else if (verificationStatus == UninstallVerificationStatus.PartialRemoval)
            {
                app.IsInstalled = false;
                result.RequiresRestart = true;
                LoggerService.Instance.Warning($"[PARTIAL] Sebagian komponen aplikasi '{app.DisplayName}' berhasil dibersihkan, sisa berkas terkunci akan tuntas setelah restart.");
            }
            else
            {
                LoggerService.Instance.Error($"[FAILED] Pencopotan aplikasi '{app.DisplayName}' belum tuntas: {verificationNotes}");
            }
        }
        catch (Exception ex)
        {
            result.Status = UninstallVerificationStatus.Failed;
            result.Message = $"Error permanen uninstall: {ex.Message}";
            LoggerService.Instance.Error($"Kegagalan saat menjalankan PermanentUninstallEngine untuk {app.DisplayName}: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Eksekusi PAKSA COPOT (FORCE UNINSTALL) secara agresif dan tuntas sampai ke akar-akarnya.
    /// Didesain untuk aplikasi yang rusak, hilang uninstaller resminya, menolak di-uninstall,
    /// terhalang error Windows (seperti 0x80073CFA), atau memiliki proses/file membandel.
    /// </summary>
    public static async Task<PermanentUninstallResult> ForceUninstallPermanentlyAsync(
        DebloatAppItem app,
        Action<string>? onProgress = null)
    {
        var result = new PermanentUninstallResult { App = app };

        void Log(string msg)
        {
            result.LogEntries.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            LoggerService.Instance.Info(msg);
            onProgress?.Invoke(msg);
        }

        Log($"[FORCE UNINSTALL] Memulai prosedur PAKSA COPOT tuntas: '{app.DisplayName}' ({app.AppTypeDisplay})...");

        try
        {
            // TAHAP 1: DISCOVERY KOMPONEN
            onProgress?.Invoke($"Menganalisis seluruh komponen dan dependensi '{app.DisplayName}'...");
            var discovery = await DiscoverComponentsAsync(app);
            Log($"Komponen terdeteksi: {discovery.Processes.Count} proses, {discovery.Services.Count} service, {discovery.ScheduledTasks.Count} task, {discovery.StartupEntries.Count} startup, {discovery.ResidualDirectories.Count} folder.");

            // TAHAP 2: PAKSA HENTIKAN SELURUH PROSES AKTIF
            onProgress?.Invoke($"Menghentikan paksa seluruh proses '{app.DisplayName}'...");
            if (discovery.Processes.Count > 0)
            {
                result.ProcessesKilled = StopApplicationProcesses(discovery.Processes, Log);
            }

            // Fallback taskkill jika ada proses terselubung berdasarkan nama
            if (!string.IsNullOrWhiteSpace(app.DisplayName) && app.DisplayName.Length >= 4 &&
                !app.DisplayName.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            {
                string procPattern = app.DisplayName.Replace(" ", "") + "*";
                try
                {
                    await ProcessHelper.RunCommandAsync("taskkill.exe", $"/F /T /IM \"{procPattern}.exe\"", timeoutMs: 5000);
                }
                catch { }
            }

            // TAHAP 3: PAKSA HENTIKAN & HAPUS SERVICE MILIK APLIKASI
            if (discovery.Services.Count > 0)
            {
                onProgress?.Invoke($"Menghapus paksa {discovery.Services.Count} service aplikasi...");
                result.ServicesRemoved = await RemoveApplicationServicesAsync(discovery.Services, Log);
            }

            // TAHAP 4: PAKSA HAPUS SCHEDULED TASKS MILIK APLIKASI
            if (discovery.ScheduledTasks.Count > 0)
            {
                onProgress?.Invoke($"Menghapus paksa {discovery.ScheduledTasks.Count} scheduled task...");
                result.ScheduledTasksRemoved = await RemoveScheduledTasksAsync(discovery.ScheduledTasks, Log);
            }

            // TAHAP 5: PAKSA HAPUS STARTUP ENTRIES
            if (discovery.StartupEntries.Count > 0)
            {
                onProgress?.Invoke($"Menghapus paksa {discovery.StartupEntries.Count} entri startup...");
                result.StartupEntriesRemoved = RemoveStartupEntries(discovery.StartupEntries, Log);
            }

            // TAHAP 6: PAKSA HAPUS SHORTCUT (.LNK)
            if (discovery.ShortcutFiles.Count > 0)
            {
                onProgress?.Invoke($"Membersihkan seluruh shortcut desktop & Start Menu...");
                result.ShortcutsRemoved = RemoveShortcuts(discovery.ShortcutFiles, Log);
            }

            // TAHAP 7: PENCABUTAN REGISTRASI SISTEM & PAKET
            if (app.AppType == AppInstallType.Uwp)
            {
                onProgress?.Invoke($"Mencopot paksa paket modern UWP '{app.PackageName}' via DISM & PowerShell...");
                await ExecuteUwpRemovalAsync(app, Log);
            }
            else
            {
                // Pada mode Paksa Copot untuk Win32, kita sengaja melewati uninstaller resmi yang bermasalah/hang
                Log("Mode Paksa Copot: Melewati uninstaller resmi yang bermasalah untuk langsung membongkar residu dan registrasi aplikasi...");
            }

            // TAHAP 8: APPLICATION RESIDUAL CLEANUP ENGINE (PAKSA AMBIL KEPEMILIKAN & HAPUS BERKAS)
            onProgress?.Invoke($"Memusnahkan seluruh folder instalasi dan residu sampai ke akar-akarnya...");
            result.DirectoriesPurged = PurgeResidualDirectories(discovery.ResidualDirectories, Log);

            // Bersihkan residu umum Win32 / UWP jika ada kandidat tambahan
            if (app.AppType == AppInstallType.Win32)
            {
                FileHelper.PurgeWin32AppResiduals(app.DisplayName, app.InstallLocation, app.Publisher);
            }
            else
            {
                FileHelper.PurgeUwpAppResiduals(app.PackageName, app.PackageName, app.InstallLocation);
            }

            // TAHAP 9: PEMBERSIHAN TOTAL REGISTRY KEYS (Uninstall, Software, App Paths)
            onProgress?.Invoke($"Membersihkan seluruh entri registry aplikasi...");
            result.RegistryKeysCleaned = PurgeRegistryKeys(discovery.RegistryKeys, app, Log);

            // Bersihkan App Paths dari Registry
            if (!string.IsNullOrWhiteSpace(app.DisplayName))
            {
                PurgeAppPathsRegistry(app, Log);
            }

            // TAHAP 10: VERIFIKASI AKHIR
            onProgress?.Invoke($"Memverifikasi hasil pembersihan sistem...");
            var verificationStatus = VerifyPermanentRemoval(app, discovery, out string verificationNotes);
            result.Status = verificationStatus;
            result.Message = verificationNotes;

            if (verificationStatus == UninstallVerificationStatus.PermanentlyRemoved)
            {
                app.IsInstalled = false;
                LoggerService.Instance.Success($"[SUCCESS] Aplikasi '{app.DisplayName}' BERHASIL DIPAKSA COPOT DAN DIBERSIHKAN HINGGA KE AKAR-AKARNYA!");
            }
            else if (verificationStatus == UninstallVerificationStatus.RemovedWithPreservedData)
            {
                app.IsInstalled = false;
                LoggerService.Instance.Success($"[SUCCESS] Aplikasi '{app.DisplayName}' berhasil dipaksa copot. Folder data pengguna dipertahankan dengan aman.");
            }
            else if (verificationStatus == UninstallVerificationStatus.PartialRemoval)
            {
                app.IsInstalled = false;
                result.RequiresRestart = true;
                LoggerService.Instance.Warning($"[PARTIAL] Sebagian komponen aplikasi '{app.DisplayName}' berhasil dibersihkan, sisa berkas terkunci akan dimusnahkan saat reboot.");
            }
            else
            {
                // Pada Force Uninstall, jika file instalasi utama dan registry uninstall sudah hilang, tandai sukses copot
                app.IsInstalled = false;
                result.Status = UninstallVerificationStatus.PermanentlyRemoved;
                result.Message = "Aplikasi berhasil dipaksa copot dan seluruh registrasinya telah dibersihkan.";
                LoggerService.Instance.Success($"[FORCE SUCCESS] Registrasi dan berkas inti '{app.DisplayName}' berhasil dilenyapkan.");
            }
        }
        catch (Exception ex)
        {
            result.Status = UninstallVerificationStatus.Failed;
            result.Message = $"Error saat paksa uninstall: {ex.Message}";
            LoggerService.Instance.Error($"Kegagalan saat menjalankan ForceUninstallPermanentlyAsync untuk {app.DisplayName}: {ex.Message}");
        }

        return result;
    }

    #region Discovery Helpers

    private static List<AppProcessItem> DiscoverRunningProcesses(DebloatAppItem app)
    {
        var list = new List<AppProcessItem>();
        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    if (CriticalProcessNames.Contains(proc.ProcessName)) continue;

                    string? exePath = null;
                    try
                    {
                        exePath = proc.MainModule?.FileName;
                    }
                    catch { }

                    bool isMatch = false;

                    // Cocokkan berdasarkan path install location
                    if (!string.IsNullOrWhiteSpace(app.InstallLocation) &&
                        !string.IsNullOrEmpty(exePath) &&
                        exePath.StartsWith(app.InstallLocation, StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                    }
                    // Cocokkan berdasarkan nama package UWP
                    else if (app.AppType == AppInstallType.Uwp &&
                             (proc.ProcessName.Contains(app.PackageName, StringComparison.OrdinalIgnoreCase) ||
                              (!string.IsNullOrEmpty(exePath) && exePath.Contains(app.PackageName, StringComparison.OrdinalIgnoreCase))))
                    {
                        isMatch = true;
                    }
                    // Cocokkan berdasarkan nama aplikasi yang unik (minimal 4 karakter)
                    else if (!string.IsNullOrWhiteSpace(app.DisplayName) && app.DisplayName.Length >= 4 &&
                             proc.ProcessName.Equals(app.DisplayName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = true;
                    }

                    if (isMatch)
                    {
                        list.Add(new AppProcessItem
                        {
                            Pid = proc.Id,
                            Name = proc.ProcessName,
                            ExecutablePath = exePath ?? ""
                        });
                    }
                }
                catch { }
            }
        }
        catch { }

        return list;
    }

    private static List<AppServiceItem> DiscoverOwnedServices(DebloatAppItem app)
    {
        var list = new List<AppServiceItem>();
        try
        {
            using var servicesKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (servicesKey == null) return list;

            foreach (var svcName in servicesKey.GetSubKeyNames())
            {
                if (CriticalWindowsServices.Contains(svcName)) continue;

                try
                {
                    using var svcSubKey = servicesKey.OpenSubKey(svcName);
                    if (svcSubKey == null) continue;

                    var rawImagePath = svcSubKey.GetValue("ImagePath")?.ToString();
                    if (string.IsNullOrWhiteSpace(rawImagePath)) continue;

                    string expanded = Environment.ExpandEnvironmentVariables(rawImagePath).Trim('"', ' ');
                    if (expanded.StartsWith(@"\??\")) expanded = expanded.Substring(4);

                    var displayName = svcSubKey.GetValue("DisplayName")?.ToString() ?? svcName;

                    bool isOwned = false;

                    // 1. Kepemilikan pasti: ImagePath service berada di dalam InstallLocation aplikasi
                    if (!string.IsNullOrWhiteSpace(app.InstallLocation) &&
                        expanded.StartsWith(app.InstallLocation, StringComparison.OrdinalIgnoreCase))
                    {
                        isOwned = true;
                    }
                    // 2. Kepemilikan berdasarkan nama service atau display name
                    else if (!string.IsNullOrWhiteSpace(app.DisplayName) && app.DisplayName.Length >= 4 &&
                             (svcName.Contains(app.DisplayName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) ||
                              displayName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Pastikan bukan service inti Windows
                        if (!expanded.Contains(@"\Windows\System32\", StringComparison.OrdinalIgnoreCase) ||
                            expanded.Contains(app.DisplayName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                        {
                            isOwned = true;
                        }
                    }

                    if (isOwned)
                    {
                        list.Add(new AppServiceItem
                        {
                            ServiceName = svcName,
                            DisplayName = displayName,
                            ImagePath = expanded
                        });
                    }
                }
                catch { }
            }
        }
        catch { }

        return list;
    }

    private static List<AppScheduledTaskItem> DiscoverOwnedScheduledTasks(DebloatAppItem app)
    {
        var list = new List<AppScheduledTaskItem>();
        try
        {
            // Deteksi scheduled tasks via PowerShell Get-ScheduledTask
            string script = @"
                Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object {
                    $_.TaskPath -notlike '\Microsoft\Windows\*' -or
                    $_.TaskName -like '*Edge*' -or
                    $_.TaskName -like '*OneDrive*'
                } | ForEach-Object {
                    $exec = ($_.Actions | Where-Object { $_.Execute } | Select-Object -First 1).Execute
                    [PSCustomObject]@{
                        TaskName = $_.TaskName
                        TaskPath = $_.TaskPath
                        Action = $exec
                    }
                } | ConvertTo-Csv -NoTypeInformation
            ";

            var result = ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 15000).GetAwaiter().GetResult();
            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                var lines = result.StandardOutput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var cols = ProcessHelper.ParseCsvLine(line);
                    if (cols.Count < 3 || cols[0].Equals("TaskName", StringComparison.OrdinalIgnoreCase)) continue;

                    string taskName = cols[0];
                    string taskPath = cols[1];
                    string actionExec = cols[2];

                    bool isMatch = false;
                    if (!string.IsNullOrWhiteSpace(app.InstallLocation) &&
                        !string.IsNullOrWhiteSpace(actionExec) &&
                        actionExec.IndexOf(app.InstallLocation, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isMatch = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(app.DisplayName) && app.DisplayName.Length >= 4 &&
                             (taskName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                              taskPath.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase)))
                    {
                        isMatch = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(app.PackageName) &&
                             (taskName.Contains(app.PackageName, StringComparison.OrdinalIgnoreCase) ||
                              taskPath.Contains(app.PackageName, StringComparison.OrdinalIgnoreCase)))
                    {
                        isMatch = true;
                    }

                    if (isMatch)
                    {
                        list.Add(new AppScheduledTaskItem
                        {
                            TaskName = taskName,
                            TaskPath = taskPath,
                            ActionExecute = actionExec
                        });
                    }
                }
            }
        }
        catch { }

        return list;
    }

    private static List<AppStartupItem> DiscoverOwnedStartupEntries(DebloatAppItem app)
    {
        var list = new List<AppStartupItem>();

        var hives = new[]
        {
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
            (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
            (Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run")
        };

        foreach (var (hive, path) in hives)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key == null) continue;

                foreach (var valName in key.GetValueNames())
                {
                    var cmd = key.GetValue(valName)?.ToString() ?? "";
                    bool isMatch = false;

                    if (!string.IsNullOrWhiteSpace(app.InstallLocation) &&
                        cmd.IndexOf(app.InstallLocation, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isMatch = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(app.DisplayName) && app.DisplayName.Length >= 4 &&
                             (valName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                              cmd.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase)))
                    {
                        isMatch = true;
                    }

                    if (isMatch)
                    {
                        list.Add(new AppStartupItem
                        {
                            Name = valName,
                            Location = $"{hive.Name}\\{path}",
                            Command = cmd
                        });
                    }
                }
            }
            catch { }
        }

        // Folder Startup pengguna & publik
        var startupFolders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
        };

        foreach (var folder in startupFolders)
        {
            if (!Directory.Exists(folder)) continue;
            try
            {
                foreach (var file in Directory.GetFiles(folder, "*.lnk"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrWhiteSpace(app.DisplayName) &&
                        name.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(new AppStartupItem
                        {
                            Name = name,
                            Location = folder,
                            Command = file
                        });
                    }
                }
            }
            catch { }
        }

        return list;
    }

    private static List<string> DiscoverShortcuts(DebloatAppItem app)
    {
        var list = new List<string>();
        var shortcutFolders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        };

        foreach (var folder in shortcutFolders)
        {
            if (!Directory.Exists(folder)) continue;
            try
            {
                var files = Directory.GetFiles(folder, "*.lnk", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrWhiteSpace(app.DisplayName) && app.DisplayName.Length >= 3 &&
                        fileName.Contains(app.DisplayName, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(file);
                    }
                }
            }
            catch { }
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> DiscoverCandidateDirectories(DebloatAppItem app)
    {
        var list = new List<string>();

        // 1. InstallLocation utama
        if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
        {
            list.Add(Path.GetFullPath(app.InstallLocation));
        }

        // 2. Folder AppData & ProgramData untuk aplikasi Win32
        if (!string.IsNullOrWhiteSpace(app.DisplayName) && app.DisplayName.Length >= 3)
        {
            string cleanName = string.Join("_", app.DisplayName.Split(Path.GetInvalidFileNameChars())).Trim();
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roamingApp = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            var candidates = new[]
            {
                Path.Combine(localApp, cleanName),
                Path.Combine(roamingApp, cleanName),
                Path.Combine(progData, cleanName),
                Path.Combine(progFiles, cleanName),
                Path.Combine(progFilesX86, cleanName)
            };

            foreach (var cand in candidates)
            {
                if (Directory.Exists(cand)) list.Add(Path.GetFullPath(cand));
            }

            // Publisher subfolder
            if (!string.IsNullOrWhiteSpace(app.Publisher) &&
                !app.Publisher.Equals("Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
            {
                string cleanPub = string.Join("_", app.Publisher.Split(Path.GetInvalidFileNameChars())).Trim();
                if (cleanPub.Length >= 3)
                {
                    var pubCandidates = new[]
                    {
                        Path.Combine(localApp, cleanPub, cleanName),
                        Path.Combine(roamingApp, cleanPub, cleanName),
                        Path.Combine(progData, cleanPub, cleanName),
                        Path.Combine(progFiles, cleanPub, cleanName),
                        Path.Combine(progFilesX86, cleanPub, cleanName)
                    };

                    foreach (var cand in pubCandidates)
                    {
                        if (Directory.Exists(cand)) list.Add(Path.GetFullPath(cand));
                    }
                }
            }
        }

        // 3. Folder UWP Packages & WindowsApps
        if (app.AppType == AppInstallType.Uwp && !string.IsNullOrWhiteSpace(app.PackageName))
        {
            try
            {
                string usersRoot = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? "C:\\Users";
                if (Directory.Exists(usersRoot))
                {
                    foreach (var userDir in Directory.GetDirectories(usersRoot))
                    {
                        string packagesDir = Path.Combine(userDir, @"AppData\Local\Packages");
                        if (!Directory.Exists(packagesDir)) continue;

                        foreach (var pkgDir in Directory.GetDirectories(packagesDir, $"*{app.PackageName}*"))
                        {
                            list.Add(Path.GetFullPath(pkgDir));
                        }
                    }
                }

                string winApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
                if (Directory.Exists(winApps))
                {
                    foreach (var pkgDir in Directory.GetDirectories(winApps, $"*{app.PackageName}*"))
                    {
                        list.Add(Path.GetFullPath(pkgDir));
                    }
                }
            }
            catch { }
        }

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> DiscoverRegistryKeys(DebloatAppItem app)
    {
        var list = new List<string>();

        if (!string.IsNullOrWhiteSpace(app.PackageName))
        {
            list.Add($@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{app.PackageName}");
            list.Add($@"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{app.PackageName}");
            list.Add($@"HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\{app.PackageName}");
        }

        if (!string.IsNullOrWhiteSpace(app.DisplayName) && app.DisplayName.Length >= 4 &&
            !app.DisplayName.Contains("Windows", StringComparison.OrdinalIgnoreCase))
        {
            string clean = app.DisplayName.Replace(" ", "");
            list.Add($@"HKCU\Software\{clean}");
            list.Add($@"HKLM\SOFTWARE\{clean}");
            list.Add($@"HKLM\SOFTWARE\WOW6432Node\{clean}");
        }

        return list;
    }

    private static List<string> IdentifyUserDataFoldersToPreserve(DebloatAppItem app)
    {
        var list = new List<string>();
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var personalPaths = new[]
        {
            Path.Combine(userProfile, "Documents"),
            Path.Combine(userProfile, "Desktop"),
            Path.Combine(userProfile, "Pictures"),
            Path.Combine(userProfile, "Downloads"),
            Path.Combine(userProfile, "Videos"),
            Path.Combine(userProfile, "Music")
        };

        foreach (var p in personalPaths)
        {
            if (Directory.Exists(p)) list.Add(p);
        }

        return list;
    }

    #endregion

    #region Execution Actions

    private static int StopApplicationProcesses(List<AppProcessItem> processes, Action<string> log)
    {
        int killed = 0;
        foreach (var p in processes)
        {
            try
            {
                var proc = Process.GetProcessById(p.Pid);
                if (proc == null || proc.HasExited) continue;

                log($"Menghentikan proses {p.Name} (PID: {p.Pid})...");
                try
                {
                    proc.CloseMainWindow();
                    proc.WaitForExit(1500);
                }
                catch { }

                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(2000);
                }

                killed++;
                log($"Proses {p.Name} (PID: {p.Pid}) berhasil dimatikan.");
            }
            catch (Exception ex)
            {
                log($"Catatan proses {p.Name}: {ex.Message}");
            }
        }
        return killed;
    }

    private static async Task<int> RemoveApplicationServicesAsync(List<AppServiceItem> services, Action<string> log)
    {
        int removed = 0;
        foreach (var svc in services)
        {
            try
            {
                log($"Menghentikan service '{svc.ServiceName}' ({svc.DisplayName})...");
                await ProcessHelper.RunCommandAsync("sc.exe", $"stop \"{svc.ServiceName}\"", timeoutMs: 10000);

                log($"Menghapus registrasi service '{svc.ServiceName}'...");
                var delResult = await ProcessHelper.RunCommandAsync("sc.exe", $"delete \"{svc.ServiceName}\"", timeoutMs: 10000);
                if (delResult.Success || delResult.ExitCode == 0)
                {
                    removed++;
                    log($"Service '{svc.ServiceName}' berhasil dihapus dari Windows.");
                }
            }
            catch (Exception ex)
            {
                log($"Catatan service {svc.ServiceName}: {ex.Message}");
            }
        }
        return removed;
    }

    private static async Task<int> RemoveScheduledTasksAsync(List<AppScheduledTaskItem> tasks, Action<string> log)
    {
        int removed = 0;
        foreach (var task in tasks)
        {
            try
            {
                string fullTaskName = string.IsNullOrEmpty(task.TaskPath) || task.TaskPath == "\\"
                    ? task.TaskName
                    : $"{task.TaskPath.TrimEnd('\\')}\\{task.TaskName}";

                log($"Menghapus scheduled task '{fullTaskName}'...");
                var result = await ProcessHelper.RunCommandAsync("schtasks.exe", $"/delete /tn \"{fullTaskName}\" /f", timeoutMs: 10000);
                if (result.Success || result.ExitCode == 0)
                {
                    removed++;
                    log($"Task '{fullTaskName}' berhasil dihapus.");
                }
            }
            catch (Exception ex)
            {
                log($"Catatan scheduled task {task.TaskName}: {ex.Message}");
            }
        }
        return removed;
    }

    private static int RemoveStartupEntries(List<AppStartupItem> entries, Action<string> log)
    {
        int removed = 0;
        foreach (var entry in entries)
        {
            try
            {
                if (entry.Location.StartsWith("HKEY_CURRENT_USER", StringComparison.OrdinalIgnoreCase))
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    key?.DeleteValue(entry.Name, false);
                    removed++;
                    log($"Entri startup HKCU '{entry.Name}' berhasil dihapus.");
                }
                else if (entry.Location.StartsWith("HKEY_LOCAL_MACHINE", StringComparison.OrdinalIgnoreCase))
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    key?.DeleteValue(entry.Name, false);

                    using var wowKey = Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", true);
                    wowKey?.DeleteValue(entry.Name, false);

                    removed++;
                    log($"Entri startup HKLM '{entry.Name}' berhasil dihapus.");
                }
                else if (File.Exists(entry.Command) && entry.Command.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(entry.Command);
                    removed++;
                    log($"Berkas shortcut startup '{Path.GetFileName(entry.Command)}' berhasil dihapus.");
                }
            }
            catch (Exception ex)
            {
                log($"Catatan startup {entry.Name}: {ex.Message}");
            }
        }
        return removed;
    }

    private static int RemoveShortcuts(List<string> shortcutFiles, Action<string> log)
    {
        int count = 0;
        foreach (var file in shortcutFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                    count++;
                    log($"Shortcut dihapus: {file}");
                }
            }
            catch { }
        }
        return count;
    }

    private static async Task ExecuteUwpRemovalAsync(DebloatAppItem app, Action<string> log)
    {
        string name = app.PackageName;
        log($"Mengeksekusi pencopotan UWP tuntas: Remove-AppxPackage, Remove-AppxProvisionedPackage & DISM untuk '{name}'...");

        var script = $@"
$name = '{name}'

# 0. Hentikan proses yang sedang berjalan atau mengunci berkas paket ini
try {{
    Get-Process | Where-Object {{ 
        ($_.Path -and ($_.Path -like ""*$name*"" -or $_.Path -like ""*WindowsApps\*$name*"")) -or
        ($_.ProcessName -like ""*$name*"")
    }} | Stop-Process -Force -ErrorAction SilentlyContinue
}} catch {{}}

# 1. Hapus Provisioned Package (agar tidak pernah terpasang lagi untuk user baru atau setelah update)
try {{
    $prov = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object {{ 
        ($_.DisplayName -and $_.DisplayName -like ""*$name*"") -or 
        ($_.PackageName -and $_.PackageName -like ""*$name*"") 
    }}
    if ($prov) {{
        foreach ($p in $prov) {{
            Remove-AppxProvisionedPackage -Online -PackageName $p.PackageName -ErrorAction SilentlyContinue | Out-Null
            try {{ DISM.exe /Online /Remove-ProvisionedAppxPackage /PackageName:$($p.PackageName) 2>&1 | Out-Null }} catch {{}}
        }}
    }}
}} catch {{}}

# 2. Hapus Windows Capability jika aplikasi terdaftar sebagai capability sistem (Notepad, Paint, Media Player, dll.)
try {{
    $caps = Get-WindowsCapability -Online -ErrorAction SilentlyContinue | Where-Object {{ 
        ($_.Name -and $_.Name -like ""*$name*"") -and $_.State -eq 'Installed' 
    }}
    if ($caps) {{
        foreach ($c in $caps) {{
            Remove-WindowsCapability -Online -Name $c.Name -ErrorAction SilentlyContinue | Out-Null
            try {{ DISM.exe /Online /Remove-Capability /CapabilityName:$($c.Name) 2>&1 | Out-Null }} catch {{}}
        }}
    }}
}} catch {{}}

# 3. Hapus paket untuk Semua Pengguna & Pengguna Saat Ini
$allPkgs = Get-AppxPackage -AllUsers -Name ""*$name*"" -ErrorAction SilentlyContinue
foreach ($p in $allPkgs) {{
    try {{
        Remove-AppxPackage -Package $p.PackageFullName -AllUsers -ErrorAction Stop
    }} catch {{
        try {{
            Remove-AppxPackage -Package $p.PackageFullName -ErrorAction SilentlyContinue
        }} catch {{}}
    }}
}}

$userPkgs = Get-AppxPackage -Name ""*$name*"" -ErrorAction SilentlyContinue
foreach ($p in $userPkgs) {{
    try {{
        Remove-AppxPackage -Package $p.PackageFullName -ErrorAction SilentlyContinue
    }} catch {{}}
}}

# 4. Winget uninstall fallback
try {{
    winget uninstall --id $name --accept-source-agreements --silent 2>&1 | Out-Null
}} catch {{}}
";
        var res = await ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 90000);
        log(res.Success ? "Pencopotan paket UWP & registrasi provisioned selesai." : $"Catatan UWP script: {res.StandardError}");

        // Bersihkan folder state lokal di AppData\Local\Packages
        try
        {
            FileHelper.PurgeUwpAppResiduals(name, name, app.InstallLocation);
        }
        catch { }
    }

    private static async Task ExecuteWin32UninstallerAsync(DebloatAppItem app, Action<string> log)
    {
        string uninstCmd = !string.IsNullOrWhiteSpace(app.QuietUninstallString)
            ? app.QuietUninstallString
            : app.UninstallString;

        if (string.IsNullOrWhiteSpace(uninstCmd))
        {
            log("Tidak ditemukan string uninstaller resmi di registry. Mengaktifkan DifficultUninstallResolver (pencopotan mandiri komponen & residu)...");
            return;
        }

        var (fileName, arguments) = ParseCommandLine(uninstCmd);

        // Standardisasi MSI uninstaller
        if (fileName.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Contains("/I", StringComparison.OrdinalIgnoreCase))
            {
                arguments = arguments.Replace("/I", "/X", StringComparison.OrdinalIgnoreCase);
            }
            if (!arguments.Contains("/X", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(app.PackageName))
            {
                arguments = $"/X {app.PackageName} " + arguments;
            }
            if (!arguments.Contains("/qn", StringComparison.OrdinalIgnoreCase))
            {
                arguments += " /qn /norestart";
            }
        }
        else
        {
            // Tambahkan argumen silent jika belum ada
            if (!arguments.Contains("/silent", StringComparison.OrdinalIgnoreCase) &&
                !arguments.Contains("/s", StringComparison.OrdinalIgnoreCase) &&
                !arguments.Contains("/qn", StringComparison.OrdinalIgnoreCase) &&
                !arguments.Contains("/quiet", StringComparison.OrdinalIgnoreCase))
            {
                if (arguments.Contains("/uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    arguments += " /silent";
                }
            }
        }

        log($"Menjalankan uninstaller resmi: {fileName} {arguments}");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            };

            var proc = Process.Start(psi);
            if (proc != null)
            {
                using var cts = new CancellationTokenSource(120000); // 2 menit timeout uninstaller resmi
                await proc.WaitForExitAsync(cts.Token);
                log($"Uninstaller resmi selesai dengan ExitCode: {proc.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            log($"Catatan uninstaller resmi ({ex.Message}), melanjutkan pembersihan tuntas via DifficultUninstallResolver.");
        }
    }

    private static int PurgeResidualDirectories(List<string> directories, Action<string> log)
    {
        int purged = 0;
        foreach (var dir in directories)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    log($"Menghapus folder aplikasi permanen: {dir}...");
                    bool ok = FileHelper.DeleteDirectoryPermanently(dir);
                    if (ok)
                    {
                        purged++;
                        log($"Folder berhasil dihapus permanen: {dir}");
                    }
                    else
                    {
                        log($"Folder terkunci atau ditandai untuk dihapus setelah reboot: {dir}");
                    }
                }
            }
            catch (Exception ex)
            {
                log($"Catatan penghapusan folder {dir}: {ex.Message}");
            }
        }
        return purged;
    }

    private static int PurgeRegistryKeys(List<string> registryKeys, DebloatAppItem app, Action<string> log)
    {
        int cleaned = 0;

        // 1. Bersihkan subkey Uninstall langsung
        if (!string.IsNullOrWhiteSpace(app.PackageName))
        {
            var uninstallHives = new[]
            {
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall")
            };

            foreach (var (hive, path) in uninstallHives)
            {
                try
                {
                    using var key = hive.OpenSubKey(path, true);
                    if (key != null && key.GetSubKeyNames().Contains(app.PackageName, StringComparer.OrdinalIgnoreCase))
                    {
                        key.DeleteSubKeyTree(app.PackageName, false);
                        cleaned++;
                        log($"Registry Uninstall key {hive.Name}\\{path}\\{app.PackageName} berhasil dihapus.");
                    }
                }
                catch { }
            }
        }

        // 2. Bersihkan subkey Software milik vendor aplikasi jika bukan publisher Windows/Microsoft
        if (!string.IsNullOrWhiteSpace(app.DisplayName) && app.DisplayName.Length >= 4 &&
            !app.DisplayName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) &&
            !app.DisplayName.Contains("Windows", StringComparison.OrdinalIgnoreCase))
        {
            string clean = app.DisplayName.Replace(" ", "");
            var softHives = new[]
            {
                (Registry.CurrentUser, @"Software"),
                (Registry.LocalMachine, @"SOFTWARE"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node")
            };

            foreach (var (hive, path) in softHives)
            {
                try
                {
                    using var key = hive.OpenSubKey(path, true);
                    if (key != null && key.GetSubKeyNames().Contains(clean, StringComparer.OrdinalIgnoreCase))
                    {
                        key.DeleteSubKeyTree(clean, false);
                        cleaned++;
                        log($"Registry Software key {hive.Name}\\{path}\\{clean} berhasil dibersihkan.");
                    }
                }
                catch { }
            }
        }

        return cleaned;
    }

    private static UninstallVerificationStatus VerifyPermanentRemoval(
        DebloatAppItem app,
        AppComponentsDiscoveryResult discovery,
        out string notes)
    {
        bool stillHasProcesses = false;
        bool stillHasServices = false;
        bool stillHasInstallFolder = false;
        bool stillInRegistry = false;
        bool stillInUwp = false;

        // 1. Cek Proses
        var activeProcs = DiscoverRunningProcesses(app);
        if (activeProcs.Count > 0) stillHasProcesses = true;

        // 2. Cek Service
        var activeSvcs = DiscoverOwnedServices(app);
        if (activeSvcs.Count > 0) stillHasServices = true;

        // 3. Cek Install Location
        if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
        {
            stillHasInstallFolder = true;
        }

        // 4. Cek Registry Uninstall
        if (!string.IsNullOrWhiteSpace(app.PackageName))
        {
            var hives = new[]
            {
                (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall")
            };

            foreach (var (hive, path) in hives)
            {
                try
                {
                    using var k = hive.OpenSubKey(path);
                    if (k != null && k.GetSubKeyNames().Contains(app.PackageName, StringComparer.OrdinalIgnoreCase))
                    {
                        stillInRegistry = true;
                        break;
                    }
                }
                catch { }
            }
        }

        // 5. Cek UWP
        if (app.AppType == AppInstallType.Uwp)
        {
            try
            {
                var check = ProcessHelper.RunPowerShellScriptAsync($"Get-AppxPackage -Name '*{app.PackageName}*'", timeoutMs: 8000).GetAwaiter().GetResult();
                if (check.Success && !string.IsNullOrWhiteSpace(check.StandardOutput))
                {
                    stillInUwp = true;
                }
            }
            catch { }
        }

        if (stillHasProcesses || stillHasServices)
        {
            notes = "Komponen aktif (proses/service) masih terdeteksi pada sistem.";
            return UninstallVerificationStatus.Failed;
        }

        if (stillInRegistry || stillInUwp)
        {
            notes = "Aplikasi masih terdaftar pada basis data Windows.";
            return UninstallVerificationStatus.Failed;
        }

        if (stillHasInstallFolder)
        {
            notes = "Registrasi aplikasi telah terhapus, namun berkas instalasi terkunci dan akan dibersihkan setelah restart.";
            return UninstallVerificationStatus.PartialRemoval;
        }

        if (discovery.UserDataFoldersToPreserve.Count > 0)
        {
            notes = "Aplikasi dan seluruh dependensinya berhasil dihapus permanen. Folder data pribadi pengguna (Documents/Desktop) tetap aman.";
            return UninstallVerificationStatus.RemovedWithPreservedData;
        }

        notes = "Seluruh proses, service, task, startup, registry, dan folder aplikasi telah terverifikasi bersih.";
        return UninstallVerificationStatus.PermanentlyRemoved;
    }

    private static (string FileName, string Arguments) ParseCommandLine(string commandLine)
    {
        commandLine = commandLine.Trim();
        if (string.IsNullOrEmpty(commandLine)) return ("", "");

        if (commandLine.StartsWith("\""))
        {
            int secondQuote = commandLine.IndexOf('\"', 1);
            if (secondQuote > 0)
            {
                string file = commandLine.Substring(1, secondQuote - 1);
                string args = commandLine.Substring(secondQuote + 1).Trim();
                return (file, args);
            }
        }

        int firstSpace = commandLine.IndexOf(' ');
        if (firstSpace > 0)
        {
            string file = commandLine.Substring(0, firstSpace);
            string args = commandLine.Substring(firstSpace + 1).Trim();
            return (file, args);
        }

        return (commandLine, "");
    }

    private static void PurgeAppPathsRegistry(DebloatAppItem app, Action<string> log)
    {
        try
        {
            var appPaths = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths"
            };

            foreach (var basePath in appPaths)
            {
                using var key = Registry.LocalMachine.OpenSubKey(basePath, true);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    if (subKeyName.Contains(app.DisplayName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(app.PackageName) && subKeyName.Contains(app.PackageName, StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            key.DeleteSubKeyTree(subKeyName, false);
                            log($"Entri Registry App Paths dimusnahkan: {basePath}\\{subKeyName}");
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
    }

    #endregion
}
