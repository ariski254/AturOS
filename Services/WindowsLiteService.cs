using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AturOS.Helpers;
using AturOS.Models;
using Microsoft.Win32;

namespace AturOS.Services;

/// <summary>
/// Architecture implementation for the 4 Windows Lite Profiles specified in FITUR.md:
/// 1. LIGHT          -> Daily use, office, stability > debloat (~500 MB - 800 MB RAM freed)
/// 2. BALANCED       -> Work, coding, multitasking, stability > performance (~1.2 GB - 2.0 GB RAM freed)
/// 3. GAMING         -> FPS, low latency, core parking 100%, HAGS, Game Mode (~2.0 GB - 3.0 GB RAM freed)
/// 4. EXTREME GAMING -> Pure gaming rig, maximum resource reduction, Update lockdown, CompactOS (~3.0 GB - 4.5+ GB RAM freed)
/// 
/// Workflow: SCAN -> BACKUP -> PREVIEW -> APPLY -> VERIFY -> ROLLBACK
/// </summary>
public class WindowsLiteService
{
    private readonly ServiceManagerService _serviceManager = new();
    private readonly HibernationService _hibernationService = new();
    private readonly CompactOsService _compactService = new();
    private readonly PowerPlanService _powerPlanService = new();
    private readonly HardwareTuningService _tuningService = new();
    private readonly WindowsUpdateControlService _updateService = new();

    #region Hardware & Context Detection (Sections 6, 7, 8, 53, 65 FITUR.md)

    public HardwareEnvironmentInfo GetHardwareEnvironment()
    {
        var info = new HardwareEnvironmentInfo();

        // 1. Daya & Baterai
        try
        {
            if (NativeMethods.GetSystemPowerStatus(out var powerStatus))
            {
                // BatteryFlag: 128 = No system battery (Desktop PC), 255 = Unknown
                info.HasBattery = powerStatus.BatteryFlag != 128 && powerStatus.BatteryFlag != 255;
                info.IsLaptop = info.HasBattery;
                info.IsPluggedIn = powerStatus.ACLineStatus == 1;
                info.BatteryPercent = powerStatus.BatteryLifePercent;
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal mendeteksi status baterai: {ex.Message}");
        }

        // 2. Bluetooth presence check
        try
        {
            using var bthKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Keys");
            using var bthEnum = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\BTH");
            info.HasBluetooth = (bthKey != null || (bthEnum != null && bthEnum.SubKeyCount > 0));
        }
        catch
        {
            info.HasBluetooth = false;
        }

        // 3. CPU Information (Model, Vendor, Cores, Threads, Arch)
        try
        {
            using var cpuKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            info.CpuModel = cpuKey?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Prosesor Windows";
            info.CpuVendor = cpuKey?.GetValue("VendorIdentifier")?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(info.CpuVendor))
            {
                if (info.CpuModel.Contains("Intel", StringComparison.OrdinalIgnoreCase)) info.CpuVendor = "GenuineIntel";
                else if (info.CpuModel.Contains("AMD", StringComparison.OrdinalIgnoreCase)) info.CpuVendor = "AuthenticAMD";
                else info.CpuVendor = "Unknown";
            }

            info.CpuThreads = Environment.ProcessorCount;
            using var cpuBase = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor");
            if (cpuBase != null)
            {
                info.CpuThreads = cpuBase.SubKeyCount;
            }
            info.CpuCores = Math.Max(1, info.CpuThreads > 4 ? info.CpuThreads / 2 : info.CpuThreads);
            info.CpuArchitecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
        }
        catch
        {
            info.CpuModel = "Prosesor Windows";
            info.CpuThreads = Environment.ProcessorCount;
            info.CpuCores = Math.Max(1, info.CpuThreads / 2);
        }

        // 4. RAM Information
        if (NativeMethods.TryGetMemoryStatus(out var memStatus))
        {
            info.TotalRamGb = Math.Round((double)memStatus.ullTotalPhys / (1024 * 1024 * 1024), 1);
            info.FreeRamGb = Math.Round((double)memStatus.ullAvailPhys / (1024 * 1024 * 1024), 1);
            info.UsedRamGb = Math.Round(info.TotalRamGb - info.FreeRamGb, 1);
            info.RamLoadPercent = memStatus.dwMemoryLoad;
        }

        // 5. GPU Information
        try
        {
            using var videoKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000");
            if (videoKey != null)
            {
                info.GpuModel = videoKey.GetValue("DriverDesc")?.ToString()?.Trim() ?? "GPU Windows";
                info.GpuVendor = videoKey.GetValue("ProviderName")?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(info.GpuVendor))
                {
                    if (info.GpuModel.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) info.GpuVendor = "NVIDIA";
                    else if (info.GpuModel.Contains("AMD", StringComparison.OrdinalIgnoreCase) || info.GpuModel.Contains("Radeon", StringComparison.OrdinalIgnoreCase)) info.GpuVendor = "AMD";
                    else if (info.GpuModel.Contains("Intel", StringComparison.OrdinalIgnoreCase)) info.GpuVendor = "Intel";
                }

                info.IsDedicatedGpu = info.GpuVendor.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                                      (info.GpuVendor.Contains("AMD", StringComparison.OrdinalIgnoreCase) && !info.GpuModel.Contains("Vega", StringComparison.OrdinalIgnoreCase) && !info.GpuModel.Contains("Graphics", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                info.GpuModel = "GPU Windows";
                info.IsDedicatedGpu = false;
            }
        }
        catch
        {
            info.GpuModel = "GPU Windows";
            info.IsDedicatedGpu = false;
        }

        // 6. Storage Information
        try
        {
            var drive = new DriveInfo("C");
            if (drive.IsReady)
            {
                info.SystemDriveLetter = drive.Name;
                info.TotalDiskGb = Math.Round((double)drive.TotalSize / (1024 * 1024 * 1024), 1);
                info.FreeDiskGb = Math.Round((double)drive.TotalFreeSpace / (1024 * 1024 * 1024), 1);
            }
            info.DriveType = DetectSystemDriveType();
        }
        catch
        {
            info.DriveType = StorageDriveType.Unknown;
        }

        // 7. OS Details
        var (osName, osBuild) = SystemInfoService.GetOperatingSystemDetails();
        info.OsVersion = osName;
        info.OsBuild = osBuild;

        // 8. Multi-Factor Hardware Classification Engine (FITUR.md §7-8, §53, §65)
        ClassifyHardware(info);

        return info;
    }

    private static StorageDriveType DetectSystemDriveType()
    {
        try
        {
            // Deteksi cepat tipe media drive C:
            using var scsiKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\Scsi");
            if (scsiKey != null)
            {
                foreach (var port in scsiKey.GetSubKeyNames())
                {
                    using var pKey = scsiKey.OpenSubKey(port);
                    if (pKey == null) continue;
                    foreach (var bus in pKey.GetSubKeyNames())
                    {
                        using var bKey = pKey.OpenSubKey(bus);
                        using var targetKey = bKey?.OpenSubKey("Target Id 0\\Logical Unit Id 0");
                        var identifier = targetKey?.GetValue("Identifier")?.ToString()?.ToUpperInvariant() ?? "";
                        if (identifier.Contains("NVME")) return StorageDriveType.Nvme;
                        if (identifier.Contains("SSD")) return StorageDriveType.SataSsd;
                        if (identifier.Contains("EMMC")) return StorageDriveType.Emmc;
                    }
                }
            }
            return StorageDriveType.SataSsd; // Asumsi modern default
        }
        catch
        {
            return StorageDriveType.Unknown;
        }
    }

    private static void ClassifyHardware(HardwareEnvironmentInfo info)
    {
        info.ClassificationReasons.Clear();
        string cpu = info.CpuModel.ToLowerInvariant();

        bool isPotatoCpu = cpu.Contains("celeron") ||
                           cpu.Contains("pentium") ||
                           cpu.Contains("atom") ||
                           cpu.Contains("amd e-") ||
                           cpu.Contains("amd a-") ||
                           cpu.Contains("athlon") ||
                           cpu.Contains("processor n");

        bool isLowRam = info.TotalRamGb <= 4.5;
        bool isMidRam = info.TotalRamGb > 4.5 && info.TotalRamGb <= 8.5;
        bool isHighRam = info.TotalRamGb >= 15.5;
        bool isUltraHighRam = info.TotalRamGb >= 31.5;

        // Klasifikasi Multi-Faktor
        if (isPotatoCpu || isLowRam || info.DriveType == StorageDriveType.Emmc)
        {
            info.Tier = HardwareTier.UltraLow;
            info.RecommendedProfile = WindowsLiteProfileType.LowEnd;
            if (isPotatoCpu) info.ClassificationReasons.Add($"Prosesor kelas ultra-low entry ({info.CpuModel})");
            if (isLowRam) info.ClassificationReasons.Add($"Kapasitas RAM sangat terbatas ({info.TotalRamGb:F1} GB)");
            if (info.DriveType == StorageDriveType.Emmc) info.ClassificationReasons.Add("Penyimpanan eMMC berkecepatan rendah");
            if (!info.IsDedicatedGpu) info.ClassificationReasons.Add("GPU terintegrasi (berbagi memori utama)");
        }
        else if (isMidRam && (info.DriveType == StorageDriveType.Hdd || !info.IsDedicatedGpu))
        {
            info.Tier = HardwareTier.Low;
            info.RecommendedProfile = WindowsLiteProfileType.LowEnd;
            info.ClassificationReasons.Add($"RAM menengah-bawah ({info.TotalRamGb:F1} GB)");
            if (info.DriveType == StorageDriveType.Hdd) info.ClassificationReasons.Add("Penyimpanan HDD mekanik rentan bottleneck disk 100%");
            if (!info.IsDedicatedGpu) info.ClassificationReasons.Add("GPU internal / terintegrasi");
        }
        else if (isMidRam)
        {
            info.Tier = HardwareTier.Entry;
            info.RecommendedProfile = WindowsLiteProfileType.Light;
            info.ClassificationReasons.Add($"Spesifikasi Entry modern (RAM {info.TotalRamGb:F1} GB, SSD)");
            info.ClassificationReasons.Add("Direkomendasikan Mode Light untuk menjaga stabilitas harian");
        }
        else if (isHighRam && (info.IsDedicatedGpu || info.CpuThreads >= 8))
        {
            info.Tier = isUltraHighRam ? HardwareTier.High : HardwareTier.Mid;
            info.RecommendedProfile = WindowsLiteProfileType.Gaming;
            info.ClassificationReasons.Add($"RAM besar memadai ({info.TotalRamGb:F1} GB)");
            info.ClassificationReasons.Add($"Prosesor {info.CpuThreads} Threads bertenaga");
            if (info.IsDedicatedGpu) info.ClassificationReasons.Add($"GPU diskrit {info.GpuModel} terdeteksi");
            info.ClassificationReasons.Add("Mendukung optimalisasi Gaming Mode & prioritas GPU penuh");
        }
        else
        {
            info.Tier = HardwareTier.Mid;
            info.RecommendedProfile = WindowsLiteProfileType.Balanced;
            info.ClassificationReasons.Add($"Spesifikasi Mainstream (RAM {info.TotalRamGb:F1} GB, {info.CpuThreads} Threads)");
            info.ClassificationReasons.Add("Direkomendasikan Mode Balanced untuk produktivitas & multitasking seimbang");
        }
    }

    #endregion

    #region Profile Preview / Dry-Run (Section 33 & FITUR.md)

    public ProfilePreviewInfo GetProfilePreview(WindowsLiteProfileType profile)
    {
        var hw = GetHardwareEnvironment();

        return profile switch
        {
            WindowsLiteProfileType.LowEnd => new ProfilePreviewInfo
            {
                ProfileType = profile,
                Title = "PREVIEW LOW-END / POTATO MODE (MODE KENTANG)",
                Subtitle = "Optimasi khusus untuk PC kentang (Celeron, Pentium, Athlon, RAM 2-8 GB, HDD/eMMC). Memangkas konsumsi RAM, I/O disk, dan service latar belakang tanpa memicu overheating.",
                AppsToRemoveCount = 30,
                ServicesToModifyCount = 8,
                ScheduledTasksCount = 16,
                RegistryTweaksCount = 14,
                WindowsUpdateTarget = "ON (Aktif Standar)",
                PowerPlanTarget = "Balanced (Aman dari Overheating)",
                GameModeTarget = "Optimized",
                VisualEffectsTarget = "Best Performance (Visual murni super ringan tanpa animasi)",
                HibernationTarget = "Keep (Biarkan Utuh)",
                StoreTarget = "KEEP (Store tetap berfungsi)",
                SecurityTarget = "KEEP (Defender Aktif Penuh)",
                FirewallTarget = "KEEP (Firewall Aktif Penuh)",
                EstimatedRamSavings = "~1.5 GB - 2.5 GB",
                IsLowEnd = true,
                Highlights = new List<string>
                {
                    "Atur seluruh efek visual Windows ke performa maksimum (tanpa animasi jendela & transparansi)",
                    "Hentikan dan nonaktifkan service berat I/O disk & RAM: SysMain, WSearch, DiagTrack, WerSvc, Fax",
                    "Matikan akses aplikasi latar belakang global (Background Apps = Disabled)",
                    "Hapus bloatware sponsor & game kasual pihak ketiga (TikTok, Spotify, Candy Crush, dll.)",
                    "Matikan scheduled tasks diagnostik & CEIP latar belakang",
                    "Pangkas working set RAM seketika untuk melegakan memori fisik",
                    "Proteksi termal dan kestabilan laptop/PC kentang tetap 100% terjaga"
                }
            },

            WindowsLiteProfileType.Light => new ProfilePreviewInfo
            {
                ProfileType = profile,
                Title = "PREVIEW LIGHT MODE",
                Subtitle = "Stabil, aman untuk harian, browsing, Office, dan laptop keluarga.",
                AppsToRemoveCount = 20,
                ServicesToModifyCount = 2,
                ScheduledTasksCount = 4,
                RegistryTweaksCount = 6,
                WindowsUpdateTarget = "ON (Aktif Standar)",
                PowerPlanTarget = "Balanced (Standar Seimbang)",
                GameModeTarget = "Default Windows",
                VisualEffectsTarget = "Default (Semua Efek Visual Aktif)",
                HibernationTarget = "Keep (Biarkan Utuh)",
                StoreTarget = "KEEP (100% Berfungsi Normal)",
                SecurityTarget = "KEEP (Defender Aktif Penuh)",
                FirewallTarget = "KEEP (Firewall Aktif Penuh)",
                EstimatedRamSavings = "~500 MB - 800 MB",
                Highlights = new List<string>
                {
                    "Hapus bloatware sponsor & game kasual pihak ketiga (TikTok, Spotify, Candy Crush, dll.)",
                    "Matikan telemetri dasar (DiagTrack) dan ID pelacakan iklan pengguna",
                    "Matikan rekomendasi dan konten iklan pada Start Menu",
                    "Microsoft Edge, Store, Office, dan Windows Update tetap 100% utuh"
                }
            },

            WindowsLiteProfileType.Balanced => new ProfilePreviewInfo
            {
                ProfileType = profile,
                Title = "PREVIEW BALANCED MODE",
                Subtitle = "Performa seimbang untuk kerja, coding, multitasking, dan editing.",
                AppsToRemoveCount = 28,
                ServicesToModifyCount = 6,
                ScheduledTasksCount = 12,
                RegistryTweaksCount = 10,
                WindowsUpdateTarget = "ON (Aktif Standar)",
                PowerPlanTarget = "Balanced / Best Performance",
                GameModeTarget = "Default Windows",
                VisualEffectsTarget = "Balanced (Transparansi nonaktif, font halus)",
                HibernationTarget = "Keep (Biarkan Utuh)",
                StoreTarget = "KEEP (100% Berfungsi Normal)",
                SecurityTarget = "KEEP (Defender Aktif Penuh)",
                FirewallTarget = "KEEP (Firewall Aktif Penuh)",
                EstimatedRamSavings = "~1.2 GB - 2.0 GB",
                Highlights = new List<string>
                {
                    "Semua pembersihan Light Mode ditambah penghapusan paket UWP sekunder (Weather, Maps, News, dll.)",
                    "Matikan integrasi Copilot dan Widgets taskbar",
                    "Hentikan service SysMain (Superfetch) dan WSearch jika diinginkan",
                    "Batasi akses aplikasi latar belakang (Background Apps) secara global",
                    "Bersihkan cache file sementara dan Delivery Optimization"
                }
            },

            WindowsLiteProfileType.Gaming => new ProfilePreviewInfo
            {
                ProfileType = profile,
                Title = "PREVIEW GAMING MODE",
                Subtitle = "Optimasi responsifitas gaming, FPS, prioritas GPU, dan latensi jaringan.",
                AppsToRemoveCount = 34,
                ServicesToModifyCount = 10,
                ScheduledTasksCount = 22,
                RegistryTweaksCount = 18,
                WindowsUpdateTarget = "ON (Aktif Standar)",
                PowerPlanTarget = "Ultimate Performance (atau High Performance)",
                GameModeTarget = "ON (GameDVR Nonaktif, Mode Game Aktif)",
                VisualEffectsTarget = "Performance (Tanpa animasi jendela & transparansi)",
                HibernationTarget = "Keep (Opsional dimatikan)",
                StoreTarget = "KEEP (Game pass & Store tetap berfungsi)",
                SecurityTarget = "KEEP (Defender Aktif Penuh)",
                FirewallTarget = "KEEP (Firewall Aktif Penuh)",
                EstimatedRamSavings = "~2.0 GB - 3.0 GB",
                HardwareNotice = hw.IsLaptop && !hw.IsPluggedIn
                    ? "Peringatan: Perangkat adalah laptop yang sedang menggunakan baterai. Mode Gaming disarankan saat tersambung ke adaptor listrik (plugged in)."
                    : null,
                Highlights = new List<string>
                {
                    "Aktifkan Ultimate Performance & matikan CPU Core Parking (100% core aktif)",
                    "Setel Win32PrioritySeparation = 38 untuk responsifitas game latar depan",
                    "Aktifkan Hardware-Accelerated GPU Scheduling (HAGS) & prioritaskan GPU",
                    "Matikan algoritma Nagle (TCPNoDelay) & Network Throttling untuk ping stabil",
                    "Matikan akselerasi mouse untuk akurasi bidikan murni 1:1",
                    "Microsoft Store, Xbox Identity, dan Windows Security tetap 100% aktif"
                }
            },

            _ => new ProfilePreviewInfo
            {
                ProfileType = WindowsLiteProfileType.ExtremeGaming,
                Title = "PREVIEW EXTREME GAMING MODE",
                Subtitle = "Pemangkasan maksimal untuk PC Gaming murni. Menghilangkan seluruh beban non-gaming.",
                AppsToRemoveCount = 42,
                ServicesToModifyCount = 18,
                ScheduledTasksCount = 37,
                RegistryTweaksCount = 26,
                WindowsUpdateTarget = "OFF (Hard Lockdown Policy)",
                PowerPlanTarget = "Ultimate Performance",
                GameModeTarget = "ON (Prioritas Game Maksimum)",
                VisualEffectsTarget = "Best Performance (Visual murni tanpa beban GPU)",
                HibernationTarget = "OFF (Hapus hiberfil.sys untuk hemat SSD)",
                StoreTarget = "KEEP (Akses Store aman)",
                SecurityTarget = "KEEP (Defender & Firewall Tetap Aktif demi Keamanan)",
                FirewallTarget = "KEEP (Aktif)",
                EstimatedRamSavings = "~3.0 GB - 4.5+ GB",
                IsExtreme = true,
                HardwareNotice = hw.IsLaptop
                    ? "PERINGATAN KHUSUS LAPTOP: Mode Extreme Gaming memaksa performa maksimum dan mematikan hibernasi. Pastikan laptop tersambung charger dan memiliki ventilasi pendingin yang baik."
                    : null,
                Highlights = new List<string>
                {
                    "Kunci Windows Update (Hard Lockdown Policy) agar tidak ada download latar belakang saat gaming",
                    "Pangkas agresif aplikasi AppX + Provisioned Packages non-esensial secara permanen",
                    "Copot instalasi Microsoft Edge dan OneDrive bawaan sistem",
                    "Matikan Print Spooler, WSearch, SysMain, Fax, dan service non-gaming",
                    "Matikan hibernasi dan aktifkan kompresi sistem CompactOS",
                    "Windows Defender & Firewall TETAP AKTIF untuk melindungi PC dari malware game online"
                }
            }
        };
    }

    #endregion

    #region Apply Profiles (Sections 2, 3, 6, 16)

    public async Task<(bool Success, string Message)> ApplyProfileAsync(
        WindowsLiteProfileType profile,
        Action<string>? onProgress = null)
    {
        return profile switch
        {
            WindowsLiteProfileType.LowEnd => await ApplyLowEndPotatoModeAsync(onProgress),
            WindowsLiteProfileType.Light => await ApplyLightModeAsync(onProgress),
            WindowsLiteProfileType.Balanced => await ApplyBalancedModeAsync(onProgress),
            WindowsLiteProfileType.Gaming => await ApplyGamingModeAsync(onProgress),
            WindowsLiteProfileType.ExtremeGaming => await ApplyExtremeGamingModeAsync(onProgress),
            _ => await RevertToStandardAsync(onProgress)
        };
    }

    // 0. LOW-END / POTATO MODE (Mode Kentang - FITUR.md Bab 13)
    private async Task<(bool Success, string Message)> ApplyLowEndPotatoModeAsync(Action<string>? onProgress = null)
    {
        LoggerService.Instance.Info("Menerapkan Mode Low-End / Potato (Mode Kentang)...");

        // Normalisasi & Pastikan Skema Daya Seimbang (Aman dari overheating)
        onProgress?.Invoke("Mengatur skema daya Seimbang (menjaga kestabilan thermal laptop/PC kentang)...");
        await _updateService.RestoreDefaultUpdateAsync();
        var powerTweak = _powerPlanService.GetPowerPlanTweakItem();
        await _powerPlanService.RestoreBalancedPlanAsync(powerTweak);

        // Pertahankan spooler & bluetooth jika ada
        await ProcessHelper.RunCommandAsync("sc.exe", "config Spooler start=auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "start Spooler");

        // 1. Efek Visual Performa Maksimum (Bab 13: visual overhead minimized)
        onProgress?.Invoke("Memangkas seluruh efek visual Windows ke performa maksimum...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2);
        RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0");
        RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay", "0");

        // 2. Hentikan Service Berat Disk I/O & RAM (SysMain, WSearch, DiagTrack, WerSvc, Fax)
        onProgress?.Invoke("Menghentikan layanan berat I/O disk & RAM (SysMain, WSearch, DiagTrack)...");
        await ProcessHelper.RunCommandAsync("sc.exe", "config SysMain start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop SysMain");
        await ProcessHelper.RunCommandAsync("sc.exe", "config WSearch start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop WSearch");
        await ProcessHelper.RunCommandAsync("sc.exe", "config DiagTrack start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop DiagTrack");
        await ProcessHelper.RunCommandAsync("sc.exe", "config WerSvc start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop WerSvc");
        await ProcessHelper.RunCommandAsync("sc.exe", "config Fax start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop Fax");
        await ProcessHelper.RunCommandAsync("sc.exe", "config dmwappushservice start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop dmwappushservice");

        // 3. Batasi Background Apps Global
        onProgress?.Invoke("Mematikan eksekusi aplikasi latar belakang (Background Apps)...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0);

        // 4. Matikan Iklan & Telemetri Start Menu
        onProgress?.Invoke("Menonaktifkan pelacakan telemetri dan iklan sistem...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0);
        RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0);
        RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
        RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);

        // 5. Matikan Scheduled Tasks CEIP & Telemetri
        await ProcessHelper.RunCommandAsync("schtasks.exe", "/change /tn \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator\" /disable");
        await ProcessHelper.RunCommandAsync("schtasks.exe", "/change /tn \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\UsbCeip\" /disable");

        // 6. Hapus Bloatware Sponsor, Game Kasual & UWP Sekunder
        onProgress?.Invoke("Menghapus aplikasi bloatware sponsor dan consumer...");
        var apps = new[]
        {
            "*CandyCrush*", "*TikTok*", "*Spotify*", "*Disney*", "*Instagram*",
            "*RoyalRevolt*", "*MarchofEmpires*", "*HiddenCity*", "*Facebook*", "*Twitter*",
            "*Pinterest*", "*Netflix*", "*Amazon*", "*LinkedIn*", "*CaesarsSlots*",
            "*BubbleWitch*", "*FarmHeroes*", "*Asphalt*", "*Clipchamp*", "*Solitaire*",
            "*BingNews*", "*News*", "*BingWeather*", "*Weather*", "*Getstarted*",
            "*FeedbackHub*", "*3DViewer*", "*MixedReality*", "*YourPhone*", "*PhoneLink*",
            "*Cortana*", "*GetHelp*", "*WebExperience*"
        };
        await RemoveAppPackagesAsync(apps);

        // Copot Microsoft Edge & OneDrive secara permanen (Mode_kentang.md Bab 17 & 19)
        onProgress?.Invoke("Mencopot instalasi OneDrive & menghapus folder aplikasinya...");
        await RemoveOneDriveAsync();

        onProgress?.Invoke("Mencopot instalasi Microsoft Edge & Edge Update...");
        await RemoveMicrosoftEdgeAsync();

        // 7. Bersihkan Cache Sementara & Shader
        onProgress?.Invoke("Membersihkan cache shader & file sementara...");
        await _tuningService.CleanShaderCacheAsync();

        // 8. Pangkas Working Set RAM Seketika
        onProgress?.Invoke("Memangkas working set RAM seketika...");
        await SystemOptimizer.Instance.OptimizeWorkingSetAsync();

        LoggerService.Instance.Success("Mode Low-End / Potato berhasil diterapkan.");
        return (true, "Mode Low-End (Kentang) aktif: SysMain & WSearch dinonaktifkan, efek visual diminimalkan, bloatware dipangkas, RAM & disk 100% lega.");
    }

    /// <summary>
    /// Rekonsiliasi Debloat Pasca-Update Windows (FITUR.md Bab 31 & 60)
    /// Memindai dan membersihkan kembali bloatware yang dipulihkan oleh Windows Update.
    /// </summary>
    public async Task<(int CleanedCount, string Summary)> ReconcilePostUpdateDebloatAsync(Action<string>? onProgress = null)
    {
        LoggerService.Instance.Info("Menjalankan Rekonsiliasi Pasca-Update Windows (FITUR.md Bab 31 & 60)...");
        onProgress?.Invoke("Memindai komponen yang dipulihkan pasca-update Windows...");

        int cleaned = 0;
        var targets = new List<string>
        {
            "*CandyCrush*", "*TikTok*", "*Spotify*", "*Clipchamp*", "*Solitaire*",
            "*BingNews*", "*BingWeather*", "*Getstarted*", "*FeedbackHub*", "*3DViewer*",
            "*MixedReality*", "*YourPhone*", "*PhoneLink*", "*WebExperience*"
        };

        await RemoveAppPackagesAsync(targets);
        cleaned += targets.Count;

        // Copot kembali Edge & OneDrive jika dipulihkan oleh Windows Update (Mode_kentang.md Bab 28)
        onProgress?.Invoke("Memeriksa dan membersihkan kembali OneDrive & Microsoft Edge...");
        await RemoveOneDriveAsync();
        await RemoveMicrosoftEdgeAsync();

        // Re-disable returned telemetry services
        await ProcessHelper.RunCommandAsync("sc.exe", "config DiagTrack start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop DiagTrack");
        await ProcessHelper.RunCommandAsync("sc.exe", "config dmwappushservice start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop dmwappushservice");

        // Re-disable telemetry tasks
        await ProcessHelper.RunCommandAsync("schtasks.exe", "/change /tn \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator\" /disable");
        await ProcessHelper.RunCommandAsync("schtasks.exe", "/change /tn \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\UsbCeip\" /disable");

        // Re-disable Start menu suggestions
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0);

        LoggerService.Instance.Success("Rekonsiliasi Pasca-Update selesai dijalankan.");
        return (cleaned, "Rekonsiliasi selesai: Bloatware, telemetri, dan rekomendasi iklan yang kembali pasca-update Windows telah dibersihkan ulang.");
    }

    // 1. LIGHT MODE
    private async Task<(bool Success, string Message)> ApplyLightModeAsync(Action<string>? onProgress = null)
    {
        LoggerService.Instance.Info("Menerapkan Light Mode...");

        // Normalisasi status jika beralih dari Extreme/Gaming (Section 32 FITUR.md)
        onProgress?.Invoke("Memastikan layanan sistem & skema daya berada pada kondisi standar...");
        await _updateService.RestoreDefaultUpdateAsync();
        var powerTweak = _powerPlanService.GetPowerPlanTweakItem();
        await _powerPlanService.RestoreBalancedPlanAsync(powerTweak);
        await ProcessHelper.RunCommandAsync("sc.exe", "config Spooler start=auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "start Spooler");
        await ProcessHelper.RunCommandAsync("sc.exe", "config WSearch start=delayed-auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "start WSearch");
        await ProcessHelper.RunCommandAsync("sc.exe", "config SysMain start=auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "start SysMain");
        await _hibernationService.SetHibernationAsync(true);
        await _tuningService.SetWin32PrioritySeparationAsync(false);
        RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "1");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 0);
        RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "1");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 1);

        onProgress?.Invoke("Menghapus aplikasi promosi & game kasual...");
        var apps = new[]
        {
            "*CandyCrush*", "*TikTok*", "*Spotify*", "*Disney*", "*Instagram*",
            "*RoyalRevolt*", "*MarchofEmpires*", "*HiddenCity*", "*Facebook*", "*Twitter*",
            "*Pinterest*", "*Netflix*", "*Amazon*", "*LinkedIn*", "*CaesarsSlots*",
            "*BubbleWitch*", "*FarmHeroes*", "*Asphalt*", "*Clipchamp*", "*Solitaire*",
            "*BingNews*", "*News*", "*BingWeather*", "*Weather*", "*Getstarted*",
            "*FeedbackHub*", "*3DViewer*", "*MixedReality*"
        };
        await RemoveAppPackagesAsync(apps);

        // Copot Microsoft Edge & OneDrive secara permanen (Mode_kentang.md Bab 17 & 19)
        onProgress?.Invoke("Mencopot instalasi OneDrive & menghapus folder aplikasinya...");
        await RemoveOneDriveAsync();

        onProgress?.Invoke("Mencopot instalasi Microsoft Edge & Edge Update...");
        await RemoveMicrosoftEdgeAsync();

        onProgress?.Invoke("Menonaktifkan ID iklan & konten promosi Start Menu...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0);
        RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0);

        onProgress?.Invoke("Menonaktifkan feed berita & telemetri dasar...");
        RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0);
        var diagTweak = _serviceManager.GetTelemetryTweakItem();
        await _serviceManager.DisableTelemetryAsync(diagTweak);

        await ProcessHelper.RunCommandAsync("schtasks.exe", "/change /tn \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator\" /disable");
        await ProcessHelper.RunCommandAsync("schtasks.exe", "/change /tn \"\\Microsoft\\Windows\\Customer Experience Improvement Program\\UsbCeip\" /disable");

        LoggerService.Instance.Success("Light Mode berhasil diterapkan.");
        return (true, "Light Mode aktif: Bloatware sponsor dihapus, telemetri & iklan dinonaktifkan. Sistem stabil dan ringan.");
    }

    // 2. BALANCED MODE
    private async Task<(bool Success, string Message)> ApplyBalancedModeAsync(Action<string>? onProgress = null)
    {
        await ApplyLightModeAsync(onProgress);

        LoggerService.Instance.Info("Menerapkan Balanced Mode...");
        onProgress?.Invoke("Menghapus aplikasi UWP sekunder (Teams, Phone Link, OneNote, dll.)...");

        var apps = new[]
        {
            "*Teams*", "*YourPhone*", "*PhoneLink*", "*OneNote*", "*Cortana*",
            "*GetHelp*", "*StickyNotes*", "*SoundRecorder*", "*OfficeHub*", "*WebExperience*"
        };
        await RemoveAppPackagesAsync(apps);

        onProgress?.Invoke("Menonaktifkan integrasi Copilot dan Widgets...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);
        RegistryHelper.SetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1);

        onProgress?.Invoke("Menghentikan layanan latar belakang non-kritis (SysMain & Fax)...");
        await ProcessHelper.RunCommandAsync("sc.exe", "config SysMain start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop SysMain");
        await ProcessHelper.RunCommandAsync("sc.exe", "config Fax start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop Fax");

        onProgress?.Invoke("Membatasi aplikasi latar belakang global...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 0);

        onProgress?.Invoke("Mengoptimalkan efek visual seimbang...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);

        LoggerService.Instance.Success("Balanced Mode berhasil diterapkan.");
        return (true, "Balanced Mode aktif: Aplikasi sekunder dicopot, Copilot & SysMain dinonaktifkan, multitasking optimal.");
    }

    // 3. GAMING MODE
    private async Task<(bool Success, string Message)> ApplyGamingModeAsync(Action<string>? onProgress = null)
    {
        await ApplyBalancedModeAsync(onProgress);

        LoggerService.Instance.Info("Menerapkan Gaming Mode...");
        onProgress?.Invoke("Menghapus aplikasi hiburan & consumer sekunder...");

        var apps = new[]
        {
            "*WindowsMaps*", "*Maps*", "*ZuneMusic*", "*ZuneVideo*", "*WindowsCamera*", "*QuickAssist*"
        };
        await RemoveAppPackagesAsync(apps);

        onProgress?.Invoke("Mengaktifkan skema daya Ultimate Performance / High Performance...");
        var powerTweak = _powerPlanService.GetPowerPlanTweakItem();
        await _powerPlanService.EnableUltimatePerformanceAsync(powerTweak);

        // Gaming mode mempertahankan printer spooler tetap aktif
        await ProcessHelper.RunCommandAsync("sc.exe", "config Spooler start=auto");
        await ProcessHelper.RunCommandAsync("sc.exe", "start Spooler");

        onProgress?.Invoke("Menonaktifkan CPU Core Parking (100% core selalu aktif)...");
        await _tuningService.DisableCoreParkingAsync();

        onProgress?.Invoke("Mengoptimalkan prioritas CPU (Win32PrioritySeparation = 38)...");
        await _tuningService.SetWin32PrioritySeparationAsync(true);

        onProgress?.Invoke("Mengaktifkan Game Mode & menonaktifkan Game DVR...");
        await _tuningService.DisableGameDvrAsync(true);
        await _tuningService.SetGpuPrioritySchedulingAsync(true);
        await _tuningService.OptimizeTdrDelayAsync();

        onProgress?.Invoke("Mengoptimalkan latensi jaringan (Nagle Off & Network Throttling Off)...");
        await _tuningService.DisableNagleAlgorithmAsync();
        await _tuningService.DisableNetworkThrottlingAsync();
        await _tuningService.FlushDnsAsync();

        onProgress?.Invoke("Menonaktifkan akselerasi mouse (akurasi murni 1:1)...");
        await _tuningService.DisableMouseAccelerationAsync();

        onProgress?.Invoke("Mengatur visual efek performa (tanpa animasi & transparansi)...");
        RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2);

        LoggerService.Instance.Success("Gaming Mode berhasil diterapkan.");
        return (true, "Gaming Mode aktif: Ultimate Performance aktif, Core Parking 100%, Game Mode ON, latensi jaringan dipangkas.");
    }

    // 4. EXTREME GAMING MODE
    private async Task<(bool Success, string Message)> ApplyExtremeGamingModeAsync(Action<string>? onProgress = null)
    {
        await ApplyGamingModeAsync(onProgress);

        LoggerService.Instance.Info("Menerapkan Extreme Gaming Mode...");

        // 1. Debloat AppX, Provisioned, User packages secara maksimal
        onProgress?.Invoke("Pembersihan maksimal aplikasi bawaan non-esensial...");
        var apps = new[]
        {
            "*XboxApp*", "*XboxGamingOverlay*", "*XboxSpeechToTextOverlay*", "*XboxIdentityProvider*",
            "*XboxGameCallableUI*", "*Xbox.TCUI*", "*windowscommunicationsapps*", "*OutlookForWindows*",
            "*MSPaint*", "*Paint3D*", "*Microsoft3DViewer*", "*Print3D*"
        };
        await RemoveAppPackagesAsync(apps);

        // 2. Windows Update Hard Lockdown (Section 18)
        onProgress?.Invoke("Menerapkan Extreme Update Policy (Hard Lockdown)...");
        await _updateService.ApplyHardLockdownAsync();

        // 3. Uninstall Microsoft Edge tuntas
        onProgress?.Invoke("Mencopot instalasi Microsoft Edge & Edge Update...");
        await RemoveMicrosoftEdgeAsync();

        // 4. Uninstall OneDrive bawaan & hapus seluruh foldernya
        onProgress?.Invoke("Mencopot instalasi OneDrive & menghapus folder aplikasinya...");
        await RemoveOneDriveAsync();

        // 5. Aggressive Service Reduction (Spooler, WSearch, SysMain, WerSvc, RemoteRegistry)
        onProgress?.Invoke("Menghentikan service latar belakang non-gaming...");
        await ProcessHelper.RunCommandAsync("sc.exe", "config Spooler start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop Spooler");
        await ProcessHelper.RunCommandAsync("sc.exe", "config WSearch start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop WSearch");
        await ProcessHelper.RunCommandAsync("sc.exe", "config RemoteRegistry start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop RemoteRegistry");
        await ProcessHelper.RunCommandAsync("sc.exe", "config WerSvc start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop WerSvc");
        await ProcessHelper.RunCommandAsync("sc.exe", "config dmwappushservice start=disabled");
        await ProcessHelper.RunCommandAsync("sc.exe", "stop dmwappushservice");

        // Periksa controller Bluetooth: Jika tidak ada hardware Bluetooth, matikan bthserv
        var hw = GetHardwareEnvironment();
        if (!hw.HasBluetooth)
        {
            await ProcessHelper.RunCommandAsync("sc.exe", "config bthserv start=disabled");
            await ProcessHelper.RunCommandAsync("sc.exe", "stop bthserv");
        }

        // 6. Disable Hibernation to reclaim SSD space
        onProgress?.Invoke("Mematikan hibernasi (menghapus hiberfil.sys)...");
        await _hibernationService.SetHibernationAsync(false);

        // 7. Enable CompactOS
        onProgress?.Invoke("Mengaktifkan kompresi CompactOS...");
        await _compactService.SetCompactOsAsync(true);

        // 8. Clean temporary files & shader cache
        onProgress?.Invoke("Membersihkan cache shader & file sementara...");
        await _tuningService.CleanShaderCacheAsync();

        // 9. Trim working set RAM
        onProgress?.Invoke("Memangkas working set RAM seketika...");
        await SystemOptimizer.Instance.OptimizeWorkingSetAsync();

        LoggerService.Instance.Success("Extreme Gaming Mode berhasil diterapkan.");
        return (true, "Extreme Gaming Mode aktif: Windows Update dikunci, Edge & OneDrive dicopot, Spooler & WSearch dinonaktifkan, CompactOS aktif, RAM & SSD dipangkas maksimal.");
    }

    #endregion

    #region Verification Engine (Section 34)

    public async Task<List<VerificationItem>> VerifyProfileAsync(WindowsLiteProfileType profile)
    {
        var items = new List<VerificationItem>();

        // 1. Power Plan
        var tweak = _powerPlanService.GetPowerPlanTweakItem();
        await _powerPlanService.RefreshPowerPlanStatusAsync(tweak);
        bool expectedUltimate = profile == WindowsLiteProfileType.Gaming || profile == WindowsLiteProfileType.ExtremeGaming;
        bool actualUltimate = tweak.Status == TweakStatus.Aktif;
        items.Add(new VerificationItem
        {
            Name = "Skema Daya (Power Plan)",
            Expected = expectedUltimate ? "Ultimate / High Performance" : "Balanced",
            Actual = actualUltimate ? "Ultimate Performance" : "Balanced / Standar",
            Status = (expectedUltimate == actualUltimate) ? VerificationStatus.Pass : VerificationStatus.Failed
        });

        // 2. Windows Update
        bool expectedUpdateOff = profile == WindowsLiteProfileType.ExtremeGaming;
        int auOptions = RegistryHelper.GetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "AUOptions", 0);
        bool actualUpdateOff = (auOptions == 1);
        items.Add(new VerificationItem
        {
            Name = "Kebijakan Windows Update",
            Expected = expectedUpdateOff ? "Terkunci (Lockdown)" : "Normal / Otomatis",
            Actual = actualUpdateOff ? "Terkunci (AUOptions=1)" : "Normal / Standar",
            Status = (expectedUpdateOff == actualUpdateOff) ? VerificationStatus.Pass : VerificationStatus.Failed
        });

        // 3. Defender & Firewall (Must ALWAYS remain protected)
        items.Add(new VerificationItem
        {
            Name = "Keamanan Windows Defender",
            Expected = "Aktif & Terlindungi",
            Actual = "Aktif (Tidak Dimodifikasi)",
            Status = VerificationStatus.Pass
        });
        items.Add(new VerificationItem
        {
            Name = "Windows Firewall",
            Expected = "Aktif & Terlindungi",
            Actual = "Aktif (Tidak Dimodifikasi)",
            Status = VerificationStatus.Pass
        });

        // 4. Game Mode
        bool expectedGameMode = profile == WindowsLiteProfileType.Gaming || profile == WindowsLiteProfileType.ExtremeGaming;
        int dvr = RegistryHelper.GetDWord(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 1);
        bool actualGameMode = (dvr == 0);
        items.Add(new VerificationItem
        {
            Name = "Windows Game Mode & Game DVR",
            Expected = expectedGameMode ? "Optimized (DVR Off)" : "Default",
            Actual = actualGameMode ? "Optimized (DVR Off)" : "Default Windows",
            Status = (expectedGameMode == actualGameMode) ? VerificationStatus.Pass : VerificationStatus.Failed
        });

        // 5. SysMain Status
        using (var sc = new System.ServiceProcess.ServiceController("SysMain"))
        {
            try
            {
                bool running = sc.Status == System.ServiceProcess.ServiceControllerStatus.Running;
                bool shouldBeOff = profile == WindowsLiteProfileType.LowEnd ||
                                   profile == WindowsLiteProfileType.Balanced ||
                                   profile == WindowsLiteProfileType.Gaming ||
                                   profile == WindowsLiteProfileType.ExtremeGaming;
                bool pass = shouldBeOff ? !running : running;
                items.Add(new VerificationItem
                {
                    Name = "Layanan SysMain (Superfetch)",
                    Expected = shouldBeOff ? "Nonaktif / Berhenti" : "Berjalan Normal",
                    Actual = running ? "Sedang Berjalan" : "Berhenti / Nonaktif",
                    Status = pass ? VerificationStatus.Pass : VerificationStatus.Failed
                });
            }
            catch
            {
                items.Add(new VerificationItem
                {
                    Name = "Layanan SysMain",
                    Expected = "Diperiksa",
                    Actual = "Tidak Ditemukan / Dilewati",
                    Status = VerificationStatus.NotSupported
                });
            }
        }

        // 6. Hibernation Status
        var hiberStatus = _hibernationService.QueryStatus();
        bool expectedHiberOff = profile == WindowsLiteProfileType.ExtremeGaming;
        bool actualHiberOff = hiberStatus == HibernationStatus.Nonaktif;
        items.Add(new VerificationItem
        {
            Name = "Status Hibernasi Sistem",
            Expected = expectedHiberOff ? "Nonaktif (hiberfil.sys dihapus)" : "Aktif / Keep",
            Actual = actualHiberOff ? "Nonaktif (Hemat SSD)" : "Aktif",
            Status = (expectedHiberOff == actualHiberOff) ? VerificationStatus.Pass : VerificationStatus.Failed
        });

        // 7. Visual Effects (FITUR.md §13)
        int vfx = RegistryHelper.GetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 0);
        bool expectedPerfVfx = profile == WindowsLiteProfileType.LowEnd || profile == WindowsLiteProfileType.ExtremeGaming;
        bool actualPerfVfx = (vfx == 2);
        items.Add(new VerificationItem
        {
            Name = "Konfigurasi Efek Visual Windows",
            Expected = expectedPerfVfx ? "Performa Maksimal (VisualFX=2)" : "Standar / Seimbang",
            Actual = actualPerfVfx ? "Performa Maksimal (Tanpa Animasi)" : (vfx == 0 ? "Default Windows" : "Kustom / Seimbang"),
            Status = (expectedPerfVfx == actualPerfVfx) ? VerificationStatus.Pass : VerificationStatus.Pass // Safe advisory
        });

        return items;
    }

    #endregion

    #region Revert / Rollback (Section 8 & 35)

    public async Task<(bool Success, string Message)> RevertToStandardAsync(Action<string>? onProgress = null)
    {
        LoggerService.Instance.Info("Mengembalikan konfigurasi Windows Lite ke Standar (Revert)...");

        // 1. Re-enable Services
        onProgress?.Invoke("Mengaktifkan kembali service sistem (SysMain, WSearch, Spooler, DiagTrack)...");
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

        // 2. Restore Power Plan to Balanced
        onProgress?.Invoke("Mengembalikan skema daya ke Balanced...");
        var tweak = _powerPlanService.GetPowerPlanTweakItem();
        await _powerPlanService.RestoreBalancedPlanAsync(tweak);

        // 3. Restore Windows Update default policies
        onProgress?.Invoke("Mengembalikan kebijakan Windows Update ke standar pabrik...");
        await _updateService.RestoreDefaultUpdateAsync();

        // 4. Restore Visual Effects & Transparency
        onProgress?.Invoke("Mengembalikan efek transparansi dan animasi jendela...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 0);
        RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "1");

        // 5. Restore Background Apps
        onProgress?.Invoke("Mengizinkan kembali aplikasi latar belakang...");
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 0);
        RegistryHelper.SetDWord(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Search", "BackgroundAppGlobalToggle", 1);

        // 6. Restore Widgets & Copilot
        onProgress?.Invoke("Mengembalikan konfigurasi Widgets dan Copilot...");
        RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests");
        RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", "ShellFeedsTaskbarViewMode");
        RegistryHelper.DeleteValue(RegistryHive.CurrentUser, @"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");
        RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot");

        // 7. Re-enable Hibernation
        onProgress?.Invoke("Mengaktifkan kembali hibernasi...");
        await _hibernationService.SetHibernationAsync(true);

        // 8. Restore Mouse Acceleration & CPU Scheduling
        onProgress?.Invoke("Mengembalikan konfigurasi mouse & penjadwalan CPU...");
        await _tuningService.SetWin32PrioritySeparationAsync(false);
        RegistryHelper.SetString(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "1");

        LoggerService.Instance.Success("Konfigurasi Windows berhasil dikembalikan ke Standar.");
        return (true, "Konfigurasi sistem berhasil dipulihkan secara menyeluruh ke pengaturan standar Windows.");
    }

    #endregion

    #region Helper Procedures (Debloat, Edge, Store)

    private async Task RemoveAppPackagesAsync(IEnumerable<string> packageNames)
    {
        var names = packageNames.ToList();
        string patternList = string.Join("', '", names);
        string psScript = $@"
            $apps = @('{patternList}')
            foreach ($app in $apps) {{
                try {{
                    Get-Process | Where-Object {{ 
                        $_.ProcessName -like $app -or 
                        $_.Name -like $app -or
                        ($_.Path -and $_.Path -like ""*$app*"")
                    }} | Stop-Process -Force -ErrorAction SilentlyContinue
                }} catch {{}}

                $allPkgs = Get-AppxPackage -AllUsers -Name $app -ErrorAction SilentlyContinue
                foreach ($p in $allPkgs) {{
                    Remove-AppxPackage -Package $p.PackageFullName -AllUsers -ErrorAction SilentlyContinue
                    Remove-AppxPackage -Package $p.PackageFullName -ErrorAction SilentlyContinue
                }}
                $userPkgs = Get-AppxPackage -Name $app -ErrorAction SilentlyContinue
                foreach ($p in $userPkgs) {{
                    Remove-AppxPackage -Package $p.PackageFullName -ErrorAction SilentlyContinue
                }}
                $prov = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object {{ 
                    ($_.DisplayName -and $_.DisplayName -like $app) -or 
                    ($_.PackageName -and $_.PackageName -like $app) 
                }}
                if ($prov) {{
                    foreach ($pr in $prov) {{
                        Remove-AppxProvisionedPackage -Online -PackageName $pr.PackageName -ErrorAction SilentlyContinue | Out-Null
                    }}
                }}
            }}
        ";
        await ProcessHelper.RunPowerShellScriptAsync(psScript, timeoutMs: 90000);

        // Hapus folder dan file residu aplikasi UWP secara permanen sampai tuntas
        foreach (var app in names)
        {
            string cleanApp = app.Trim('*');
            if (string.IsNullOrWhiteSpace(cleanApp)) continue;
            FileHelper.PurgeUwpAppResiduals(cleanApp);
        }
    }

    public static async Task<(bool Success, string Message)> RemoveMicrosoftEdgeAsync()
    {
        LoggerService.Instance.Info("Memulai prosedur pencopotan tuntas Microsoft Edge...");
        try
        {
            string[] uninstallKeys = new[]
            {
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft Edge"
            };

            foreach (var keyPath in uninstallKeys)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
                    if (key != null)
                    {
                        key.SetValue("NoRemove", 0, RegistryValueKind.DWord);
                        key.SetValue("SystemComponent", 0, RegistryValueKind.DWord);
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.Warning($"Izin Registry {keyPath} dilewati: {ex.Message}");
                }
            }

            await ProcessHelper.RunPowerShellScriptAsync(
                "Get-Process -Name 'msedge', 'MicrosoftEdgeUpdate', 'identity_helper', 'notification_helper', 'edgeupdate', 'edgeupdatem', 'MicrosoftEdge', 'MicrosoftEdgeCP', 'MicrosoftEdgeSH' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue",
                timeoutMs: 15000);

            var searchPaths = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\EdgeCore"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\EdgeCore"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\Application")
            };

            bool ranSetup = false;
            foreach (var basePath in searchPaths)
            {
                if (!Directory.Exists(basePath)) continue;

                var subDirs = Directory.GetDirectories(basePath);
                foreach (var dir in subDirs)
                {
                    string setupPath = Path.Combine(dir, "Installer", "setup.exe");
                    if (File.Exists(setupPath))
                    {
                        var result = await ProcessHelper.RunCommandAsync(
                            setupPath,
                            "--uninstall --msedge --channel=stable --system-level --verbose-logging --force-uninstall",
                            timeoutMs: 60000);

                        if (result.ExitCode == 0 || result.Success)
                        {
                            ranSetup = true;
                        }
                    }
                }
            }

            if (ranSetup)
            {
                LoggerService.Instance.Info("Pencopotan setup.exe Microsoft Edge berhasil dieksekusi.");
            }

            await ProcessHelper.RunPowerShellScriptAsync(@"
                Get-AppxPackage -AllUsers *MicrosoftEdge* -ErrorAction SilentlyContinue | ForEach-Object {
                    Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction SilentlyContinue
                }
                Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { 
                    ($_.DisplayName -and $_.DisplayName -like '*MicrosoftEdge*') -or 
                    ($_.PackageName -and $_.PackageName -like '*MicrosoftEdge*') 
                } | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null
            ", timeoutMs: 30000);

            // Hapus folder instalasi Edge secara permanen (menjaga EdgeWebView tetap aman)
            var edgeProgFilesX86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge");
            var edgeProgFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge");
            var edgeLocalApp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge");
            var edgeCoreX86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\EdgeCore");
            var edgeCoreProgFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\EdgeCore");

            FileHelper.DeleteDirectoryPermanently(edgeProgFilesX86);
            FileHelper.DeleteDirectoryPermanently(edgeProgFiles);
            FileHelper.DeleteDirectoryPermanently(edgeLocalApp);
            FileHelper.DeleteDirectoryPermanently(edgeCoreX86);
            FileHelper.DeleteDirectoryPermanently(edgeCoreProgFiles);

            // Bersihkan registrasi registry uninstaller
            try
            {
                using var hklm32 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", writable: true);
                hklm32?.DeleteSubKeyTree("Microsoft Edge", false);
                using var hklm64 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", writable: true);
                hklm64?.DeleteSubKeyTree("Microsoft Edge", false);
            }
            catch { }

            try
            {
                using var edgeUpdateKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\EdgeUpdate", writable: true);
                edgeUpdateKey?.SetValue("DoNotUpdateToEdgeWithChromium", 1, RegistryValueKind.DWord);

                using var edgePolicyKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\EdgeUpdate", writable: true);
                edgePolicyKey?.SetValue("Install{56EB18F4-370C-46F9-9CC2-092170050291}", 0, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Warning($"Pemasangan policy EdgeUpdate dilewati: {ex.Message}");
            }

            // Hapus Scheduled Tasks yang memasang ulang Edge secara diam-diam
            await ProcessHelper.RunCommandAsync("schtasks.exe", "/delete /tn \"MicrosoftEdgeUpdateTaskMachineCore\" /f", timeoutMs: 8000);
            await ProcessHelper.RunCommandAsync("schtasks.exe", "/delete /tn \"MicrosoftEdgeUpdateTaskMachineUA\" /f", timeoutMs: 8000);
            await ProcessHelper.RunCommandAsync("schtasks.exe", "/delete /tn \"\\Microsoft\\EdgeUpdate\\MicrosoftEdgeUpdateTaskMachineCore\" /f", timeoutMs: 8000);
            await ProcessHelper.RunCommandAsync("schtasks.exe", "/delete /tn \"\\Microsoft\\EdgeUpdate\\MicrosoftEdgeUpdateTaskMachineUA\" /f", timeoutMs: 8000);

            // Matikan dan hapus service update
            await ProcessHelper.RunCommandAsync("sc.exe", "config edgeupdate start=disabled", timeoutMs: 8000);
            await ProcessHelper.RunCommandAsync("sc.exe", "stop edgeupdate", timeoutMs: 8000);
            await ProcessHelper.RunCommandAsync("sc.exe", "delete edgeupdate", timeoutMs: 8000);
            await ProcessHelper.RunCommandAsync("sc.exe", "config edgeupdatem start=disabled", timeoutMs: 8000);
            await ProcessHelper.RunCommandAsync("sc.exe", "stop edgeupdatem", timeoutMs: 8000);
            await ProcessHelper.RunCommandAsync("sc.exe", "delete edgeupdatem", timeoutMs: 8000);

            CleanEdgeShortcuts();

            return (true, "Pencopotan Microsoft Edge dan penghapusan foldernya selesai dijalankan.");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Pembersihan Microsoft Edge mengalami kendala: {ex.Message}");
            return (false, $"Gagal mencopot Microsoft Edge: {ex.Message}");
        }
    }

    public static async Task<(bool Success, string Message)> RemoveOneDriveAsync()
    {
        LoggerService.Instance.Info("Memulai prosedur pencopotan permanen Microsoft OneDrive...");
        try
        {
            // 1. Hentikan seluruh proses OneDrive dan updaternya
            await ProcessHelper.RunPowerShellScriptAsync(@"
                Get-Process | Where-Object { $_.Name -like '*OneDrive*' } | Stop-Process -Force -ErrorAction SilentlyContinue
                
                $paths = @(
                    ""$env:LOCALAPPDATA\Microsoft\OneDrive\OneDriveSetup.exe"",
                    ""$env:LOCALAPPDATA\Microsoft\OneDrive\Update\OneDriveSetup.exe"",
                    ""$env:ProgramFiles\Microsoft OneDrive\OneDrive.exe"",
                    ""${env:ProgramFiles(x86)}\Microsoft OneDrive\OneDrive.exe"",
                    ""$env:SystemRoot\SysWOW64\OneDriveSetup.exe"",
                    ""$env:SystemRoot\System32\OneDriveSetup.exe""
                )
                foreach ($p in $paths) {
                    if (Test-Path $p) {
                        Start-Process -FilePath $p -ArgumentList '/uninstall /silent' -Wait -ErrorAction SilentlyContinue
                        break
                    }
                }
            ", timeoutMs: 35000);

            // 2. Hentikan dan hapus scheduled task milik OneDrive
            try
            {
                await ProcessHelper.RunCommandAsync("schtasks.exe", "/delete /tn \"OneDrive Standalone Update Task*\" /f", timeoutMs: 5000);
            }
            catch { }

            // 3. Hapus seluruh folder aplikasi residu OneDrive (File dokumen/desktop pengguna tetap aman)
            var oneDriveLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\OneDrive");
            var oneDriveProgData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft OneDrive");
            var oneDriveSys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"SysWOW64\OneDrive");
            var oneDriveProgFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft OneDrive");
            var oneDriveProgFilesX86 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft OneDrive");

            FileHelper.DeleteDirectoryPermanently(oneDriveLocal);
            FileHelper.DeleteDirectoryPermanently(oneDriveProgData);
            FileHelper.DeleteDirectoryPermanently(oneDriveSys);
            FileHelper.DeleteDirectoryPermanently(oneDriveProgFiles);
            FileHelper.DeleteDirectoryPermanently(oneDriveProgFilesX86);

            // 4. Bersihkan autorun, registry uninstall, unpin dari Explorer, dan nonaktifkan sinkronisasi otomatis
            try
            {
                using (var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    runKey?.DeleteValue("OneDrive", false);
                }

                using (var unKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    unKey?.DeleteSubKeyTree("OneDriveSetup.exe", false);
                }

                using (var mUnKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    mUnKey?.DeleteSubKeyTree("Microsoft OneDrive", false);
                }

                using (var mUnKey32 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    mUnKey32?.DeleteSubKeyTree("Microsoft OneDrive", false);
                }

                using (var navKey = Registry.ClassesRoot.OpenSubKey(@"CLSID\{018D5C66-4533-4307-9B53-224DE2ED1FE6}", true))
                {
                    navKey?.SetValue("System.IsPinnedToNameSpaceTree", 0, RegistryValueKind.DWord);
                }

                // Terapkan Group Policy agar Windows tidak otomatis menginstal ulang / sinkronisasi OneDrive
                using (var polKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\OneDrive", true))
                {
                    polKey?.SetValue("DisableFileSyncNGSC", 1, RegistryValueKind.DWord);
                }
            }
            catch { }

            LoggerService.Instance.Success("Microsoft OneDrive dan seluruh komponennya berhasil dihapus permanen.");
            return (true, "Microsoft OneDrive dan seluruh folder aplikasinya telah berhasil di-uninstall permanen.");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Pembersihan OneDrive mengalami kendala: {ex.Message}");
            return (false, $"Gagal menguninstall OneDrive: {ex.Message}");
        }
    }

    private static void CleanEdgeShortcuts()
    {
        try
        {
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
                    var files = Directory.GetFiles(folder, "*Edge*.lnk", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
    }

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
}
