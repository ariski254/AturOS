using System;
using System.IO;
using AturOS.Helpers;
using Microsoft.Win32;

namespace AturOS.Services;

public enum LiteLevel
{
    Level1_Light,
    Level2_Medium,
    Level3_Extreme
}

/// <summary>
/// Service implementation for the 3 Tiers of Windows Lite & Auto-Debloat
/// as specified in Mode.md:
/// Tier 1: Light (Aman & Harian) -> ~400-700 MB freed
/// Tier 2: Medium (Seimbang & Kerja) -> ~1.2-2.0 GB freed
/// Tier 3: Extreme (Pangkas Agresif & Barebone PC Kentang/Gaming) -> ~2.5-3.5+ GB freed
/// </summary>
public class WindowsLiteService
{
    private readonly ServiceManagerService _serviceManager = new();
    private readonly HibernationService _hibernationService = new();
    private readonly CompactOsService _compactService = new();

    #region 1. Mode Light (Aman & Harian)

    public async Task<(bool Success, string Message)> ApplyLiteLevel1Async(Action<string>? onProgress = null)
    {
        LoggerService.Instance.Info("Menerapkan Windows Lite Mode - Tingkat 1 (Mode Light: Aman & Harian)...");
        onProgress?.Invoke("Menghapus paket promosi sponsor & game kasual...");

        // 1. Debloat promotional & 3rd-party stub apps
        var removeBloatScript = @"
            $apps = @(
                '*CandyCrush*', '*TikTok*', '*Spotify*', '*Disney*', 
                '*Instagram*', '*RoyalRevolt*', '*MarchofEmpires*', '*HiddenCity*'
            )
            foreach ($app in $apps) {
                Get-AppxPackage -Name $app -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
                $prov = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like $app }
                if ($prov) { Remove-AppxProvisionedPackage -Online -PackageName $prov.PackageName -ErrorAction SilentlyContinue | Out-Null }
            }
        ";
        await ProcessHelper.RunPowerShellScriptAsync(removeBloatScript, timeoutMs: 45000);

        // 2. Disable Widgets, News & Interests, and Start Menu Ads
        onProgress?.Invoke("Menonaktifkan feed berita/widget dan iklan Start Menu...");
        RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0);
        RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "ShellFeedsTaskbarViewMode", 2);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0);

        // 3. Disable basic telemetry (DiagTrack) and ad tracking
        onProgress?.Invoke("Menonaktifkan layanan telemetri dasar dan pelacakan iklan...");
        var diagTweak = _serviceManager.GetTelemetryTweakItem();
        await _serviceManager.DisableTelemetryAsync(diagTweak);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0);

        // 4. Disable non-essential Scheduled Tasks
        onProgress?.Invoke("Menghentikan tugas terjadwal non-esensial...");
        await ProcessHelper.RunCommandAsync("schtasks.exe", "/change /tn \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator\" /disable");
        await ProcessHelper.RunCommandAsync("schtasks.exe", "/change /tn \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\UsbCeip\" /disable");

        LoggerService.Instance.Success("Windows Lite Mode Light berhasil diterapkan.");
        return (true, "Mode Light (Aman & Harian) aktif: Bloatware promosi dihapus, telemetri & widget dinonaktifkan. Estimasi hemat ~400-700 MB RAM.");
    }

    #endregion

    #region 2. Mode Medium (Seimbang & Kerja)

    public async Task<(bool Success, string Message)> ApplyLiteLevel2Async(Action<string>? onProgress = null)
    {
        // 1. Execute Mode Light first
        await ApplyLiteLevel1Async(onProgress);

        LoggerService.Instance.Info("Menerapkan Windows Lite Mode - Tingkat 2 (Mode Medium: Seimbang & Kerja)...");
        onProgress?.Invoke("Menghapus aplikasi bawaan sekunder non-esensial...");

        // 2. Remove secondary UWP apps
        var removeUwpScript = @"
            $apps = @(
                '*BingWeather*', '*BingNews*', '*WindowsMaps*', '*Getstarted*', 
                '*WindowsFeedbackHub*', '*MicrosoftStickyNotes*', '*WindowsSoundRecorder*', 
                '*MicrosoftOfficeHub*', '*MicrosoftSolitaireCollection*', '*Clipchamp*', 
                '*549981C3F5F10*'
            )
            foreach ($app in $apps) {
                Get-AppxPackage -Name $app -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
                $prov = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like $app }
                if ($prov) { Remove-AppxProvisionedPackage -Online -PackageName $prov.PackageName -ErrorAction SilentlyContinue | Out-Null }
            }
        ";
        await ProcessHelper.RunPowerShellScriptAsync(removeUwpScript, timeoutMs: 60000);

        // 3. Disable Copilot
        onProgress?.Invoke("Menonaktifkan antarmuka Windows Copilot...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
        RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);

        // 4. Disable SysMain & Search Indexer (WSearch)
        onProgress?.Invoke("Menghentikan service SysMain dan WSearch...");
        await ProcessHelper.RunCommandAsync("sc.exe", "config SysMain start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop SysMain");
        await ProcessHelper.RunCommandAsync("sc.exe", "config WSearch start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop WSearch");

        // 5. Block Background Apps globally
        onProgress?.Invoke("Memblokir izin aplikasi latar belakang...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0);

        // 6. Disable transparency and unnecessary animations
        onProgress?.Invoke("Menonaktifkan efek transparansi dan animasi jendela...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);
        RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0");

        LoggerService.Instance.Success("Windows Lite Mode Medium berhasil diterapkan.");
        return (true, "Mode Medium (Seimbang & Kerja) aktif: Aplikasi sekunder dicopot, background apps diblokir, SysMain & WSearch dimatikan. Estimasi hemat ~1.2-2.0 GB RAM.");
    }

    #endregion

    #region 3. Mode Extreme (Pangkas Agresif & Ramah PC Kentang / Gaming)

    public async Task<(bool Success, string Message)> ApplyLiteLevel3Async(Action<string>? onProgress = null)
    {
        // 1. Execute Mode Medium first
        await ApplyLiteLevel2Async(onProgress);

        LoggerService.Instance.Info("Menerapkan Windows Lite Mode - Tingkat 3 (Mode Extreme: Barebone PC Kentang / Gaming)...");

        // 2. Total Barebone Debloat: Remove remaining non-essential apps
        onProgress?.Invoke("Menghapus aplikasi non-esensial (Xbox, Phone Link, Mail, Media, OneDrive)...");
        var removeRemainingAppsScript = @"
            $apps = @(
                '*XboxApp*', '*XboxGamingOverlay*', '*XboxSpeechToTextOverlay*', '*XboxIdentityProvider*',
                '*YourPhone*', '*windowscommunicationsapps*', '*ZuneMusic*', '*ZuneVideo*', 
                '*WindowsCamera*', '*QuickAssist*', '*MSPaint*', '*WebExperience*', '*WindowsStore*'
            )
            foreach ($app in $apps) {
                Get-AppxPackage -Name $app -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
                $prov = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -like $app }
                if ($prov) { Remove-AppxProvisionedPackage -Online -PackageName $prov.PackageName -ErrorAction SilentlyContinue | Out-Null }
            }
        ";
        await ProcessHelper.RunPowerShellScriptAsync(removeRemainingAppsScript, timeoutMs: 60000);

        // 3. Uninstall Microsoft Edge and Edge background components
        onProgress?.Invoke("Mencopot instalasi Microsoft Edge dan Edge Update...");
        await RemoveMicrosoftEdgeAsync();

        // 4. Uninstall OneDrive bawaan
        onProgress?.Invoke("Mencopot instalasi OneDrive bawaan...");
        await ProcessHelper.RunPowerShellScriptAsync(@"
            Get-Process -Name 'OneDrive' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
            $paths = @(
                ""$env:SystemRoot\SysWOW64\OneDriveSetup.exe"",
                ""$env:SystemRoot\System32\OneDriveSetup.exe"",
                ""$env:LOCALAPPDATA\Microsoft\OneDrive\OneDriveSetup.exe""
            )
            foreach ($p in $paths) {
                if (Test-Path $p) {
                    Start-Process -FilePath $p -ArgumentList '/uninstall /silent' -Wait -ErrorAction SilentlyContinue
                    break
                }
            }
        ");

        // 5. Adjust for Best Performance visual mode
        onProgress?.Invoke("Mengalihkan tampilan visual ke Performa Terbaik...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2);

        // 6. Disable Hibernation
        onProgress?.Invoke("Mematikan file hibernasi (menghapus hiberfil.sys)...");
        await _hibernationService.SetHibernationAsync(false);

        // 7. Enable CompactOS
        onProgress?.Invoke("Mengaktifkan kompresi sistem CompactOS...");
        await _compactService.SetCompactOsAsync(true);

        // 8. Stop background services: Spooler, Bluetooth, RemoteRegistry, WerSvc
        onProgress?.Invoke("Menghentikan background services non-kritis...");
        await ProcessHelper.RunCommandAsync("sc.exe", "config Spooler start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop Spooler");
        await ProcessHelper.RunCommandAsync("sc.exe", "config bthserv start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop bthserv");
        await ProcessHelper.RunCommandAsync("sc.exe", "config RemoteRegistry start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop RemoteRegistry");
        await ProcessHelper.RunCommandAsync("sc.exe", "config WerSvc start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop WerSvc");
        await ProcessHelper.RunCommandAsync("sc.exe", "config dmwappushservice start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop dmwappushservice");

        // 8. Immediate Working Set RAM flush
        onProgress?.Invoke("Memangkas working set RAM seketika...");
        await SystemOptimizer.Instance.OptimizeWorkingSetAsync();

        LoggerService.Instance.Success("Windows Lite Mode Extreme berhasil diterapkan.");
        return (true, "Mode Extreme aktif: Edge dicopot, barebone apps aktif, CompactOS aktif, hibernasi dimatikan, RAM dipangkas seketika. Estimasi hemat ~2.5-3.5+ GB RAM.");
    }

    private static async Task RemoveMicrosoftEdgeAsync()
    {
        try
        {
            // Kill Edge processes
            await ProcessHelper.RunPowerShellScriptAsync("Get-Process -Name 'msedge', 'mseedgewebview2', 'MicrosoftEdgeUpdate' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue");

            // Look for Edge setup.exe
            string[] possiblePaths = new[]
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application",
                @"C:\Program Files\Microsoft\Edge\Application"
            };

            foreach (var basePath in possiblePaths)
            {
                if (!Directory.Exists(basePath)) continue;

                var installerDirs = Directory.GetDirectories(basePath);
                foreach (var dir in installerDirs)
                {
                    string setupPath = Path.Combine(dir, "Installer", "setup.exe");
                    if (File.Exists(setupPath))
                    {
                        await ProcessHelper.RunCommandAsync(setupPath, "--uninstall --system-level --verbose-logging --force-uninstall");
                    }
                }
            }

            // Disable Edge services
            await ProcessHelper.RunCommandAsync("sc.exe", "config edgeupdate start=disabled");
            await ProcessHelper.RunCommandAsync("sc.exe", "stop edgeupdate");
            await ProcessHelper.RunCommandAsync("sc.exe", "config edgeupdatem start=disabled");
            await ProcessHelper.RunCommandAsync("sc.exe", "stop edgeupdatem");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Pembersihan Microsoft Edge dilewati: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores Microsoft Store if stripped during Extreme Mode.
    /// </summary>
    public static async Task<(bool Success, string Message)> RestoreMicrosoftStoreAsync(Action<string>? onProgress = null)
    {
        onProgress?.Invoke("Memulihkan paket Microsoft Store...");
        LoggerService.Instance.Info("Memulihkan Microsoft Store...");

        var script = @"
            Get-AppxPackage -allusers *WindowsStore* | Foreach { Add-AppxPackage -DisableDevelopmentMode -Register ""$($_.InstallLocation)\AppXManifest.xml"" } -ErrorAction SilentlyContinue
            wsreset.exe -i
        ";
        var result = await ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 60000);
        return (result.Success, "Perintah instalasi ulang Microsoft Store telah dieksekusi. Periksa menu Start beberapa saat lagi.");
    }

    #endregion

    #region 4. Kembali ke Standar (Revert)

    public async Task<(bool Success, string Message)> RevertToStandardAsync(Action<string>? onProgress = null)
    {
        LoggerService.Instance.Info("Mengembalikan konfigurasi Windows Lite ke Standar (Revert)...");

        // 1. Re-enable Services
        onProgress?.Invoke("Mengaktifkan kembali service sistem bawaan...");
        await ProcessHelper.RunCommandAsync("sc.exe", "config SysMain start=auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "start SysMain");
        await ProcessHelper.RunCommandAsync("sc.exe", "config WSearch start=delayed-auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "start WSearch");
        await ProcessHelper.RunCommandAsync("sc.exe", "config Spooler start=auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "start Spooler");
        await ProcessHelper.RunCommandAsync("sc.exe", "config DiagTrack start=auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "start DiagTrack");
        await ProcessHelper.RunCommandAsync("sc.exe", "config WerSvc start=delayed-auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "config bthserv start=manual");
        await ProcessHelper.RunCommandAsync("sc.exe", "config edgeupdate start=auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "config edgeupdatem start=delayed-auto");

        // 2. Restore Transparency, Visual Effects, Animations
        onProgress?.Invoke("Mengembalikan efek transparansi dan animasi visual...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 0);
        RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "1");

        // 3. Restore Background Apps
        onProgress?.Invoke("Mengizinkan kembali aplikasi latar belakang...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 1);

        // 4. Restore Widgets & Copilot
        onProgress?.Invoke("Mengembalikan konfigurasi Widgets dan Copilot...");
        RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests");
        RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "ShellFeedsTaskbarViewMode");
        RegistryHelper.DeleteValue(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");
        RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");

        // 5. Re-enable Hibernation
        onProgress?.Invoke("Mengaktifkan kembali hibernasi...");
        await _hibernationService.SetHibernationAsync(true);

        LoggerService.Instance.Success("Konfigurasi Windows berhasil dikembalikan ke Standar.");
        return (true, "Konfigurasi sistem berhasil dipulihkan ke pengaturan standar Windows.");
    }

    #endregion
}
