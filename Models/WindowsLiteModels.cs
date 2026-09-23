using System;
using System.Collections.Generic;

namespace AturOS.Models;

public enum WindowsLiteProfileType
{
    Standard = 0,
    Light = 1,
    Balanced = 2,
    Gaming = 3,
    ExtremeGaming = 4,
    LowEnd = 5
}

public enum HardwareTier
{
    UltraLow = 0,
    Low = 1,
    Entry = 2,
    Mid = 3,
    High = 4
}

public enum StorageDriveType
{
    Unknown = 0,
    Hdd = 1,
    SataSsd = 2,
    Nvme = 3,
    Emmc = 4
}

public enum VerificationStatus
{
    Pass,
    Failed,
    NotSupported,
    Skipped
}

public class VerificationItem
{
    public string Name { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
    public VerificationStatus Status { get; set; }
    public string StatusText => Status switch
    {
        VerificationStatus.Pass => "PASS",
        VerificationStatus.Failed => "GAGAL",
        VerificationStatus.NotSupported => "TIDAK DIDUKUNG",
        _ => "DILEWATI"
    };

    public string StatusForeground => Status switch
    {
        VerificationStatus.Pass => "#16A34A",
        VerificationStatus.Failed => "#DC2626",
        VerificationStatus.NotSupported => "#64748B",
        _ => "#D97706"
    };

    public string StatusBackground => Status switch
    {
        VerificationStatus.Pass => "#DCFCE7",
        VerificationStatus.Failed => "#FEE2E2",
        VerificationStatus.NotSupported => "#F1F5F9",
        _ => "#FEF3C7"
    };
}

public class ProfilePreviewInfo
{
    public WindowsLiteProfileType ProfileType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public int AppsToRemoveCount { get; set; }
    public int ServicesToModifyCount { get; set; }
    public int ScheduledTasksCount { get; set; }
    public int RegistryTweaksCount { get; set; }
    public string WindowsUpdateTarget { get; set; } = "ON (Aktif Standar)";
    public string PowerPlanTarget { get; set; } = "Balanced";
    public string GameModeTarget { get; set; } = "Default";
    public string VisualEffectsTarget { get; set; } = "Default Windows";
    public string HibernationTarget { get; set; } = "Keep (Biarkan)";
    public string StoreTarget { get; set; } = "KEEP (Dipertahankan)";
    public string SecurityTarget { get; set; } = "KEEP (Aktif & Terlindungi)";
    public string FirewallTarget { get; set; } = "KEEP (Aktif)";
    public string EstimatedRamSavings { get; set; } = string.Empty;
    public List<string> Highlights { get; set; } = new();
    public bool RequiresElevation { get; set; } = true;
    public bool IsExtreme { get; set; } = false;
    public bool IsLowEnd { get; set; } = false;
    public string? HardwareNotice { get; set; }
}

public class HardwareEnvironmentInfo
{
    // CPU
    public string CpuModel { get; set; } = string.Empty;
    public string CpuVendor { get; set; } = string.Empty;
    public int CpuCores { get; set; }
    public int CpuThreads { get; set; }
    public string CpuArchitecture { get; set; } = "x64";

    // RAM
    public double TotalRamGb { get; set; }
    public double FreeRamGb { get; set; }
    public double UsedRamGb { get; set; }
    public double RamLoadPercent { get; set; }

    // GPU
    public string GpuModel { get; set; } = string.Empty;
    public string GpuVendor { get; set; } = string.Empty;
    public bool IsDedicatedGpu { get; set; }

    // Storage
    public StorageDriveType DriveType { get; set; } = StorageDriveType.Unknown;
    public string DriveTypeDisplay => DriveType switch
    {
        StorageDriveType.Nvme => "NVMe SSD",
        StorageDriveType.SataSsd => "SATA SSD",
        StorageDriveType.Hdd => "HDD Mekanik",
        StorageDriveType.Emmc => "eMMC Flash",
        _ => "Penyimpanan Standar"
    };
    public string SystemDriveLetter { get; set; } = "C:";
    public double TotalDiskGb { get; set; }
    public double FreeDiskGb { get; set; }

    // System & Power
    public bool IsLaptop { get; set; }
    public bool HasBattery { get; set; }
    public bool IsPluggedIn { get; set; }
    public byte BatteryPercent { get; set; }
    public bool HasBluetooth { get; set; }
    public string OsVersion { get; set; } = string.Empty;
    public string OsBuild { get; set; } = string.Empty;

    // Classification & Recommendation (FITUR.md §6-8, §53, §65)
    public HardwareTier Tier { get; set; } = HardwareTier.Mid;
    public string TierName => Tier switch
    {
        HardwareTier.UltraLow => "ULTRA LOW",
        HardwareTier.Low => "LOW (KENTANG)",
        HardwareTier.Entry => "ENTRY LEVEL",
        HardwareTier.Mid => "MAINSTREAM (MID)",
        HardwareTier.High => "HIGH END",
        _ => "STANDAR"
    };

    public string TierForeground => Tier switch
    {
        HardwareTier.UltraLow => "#DC2626",
        HardwareTier.Low => "#EA580C",
        HardwareTier.Entry => "#D97706",
        HardwareTier.Mid => "#2563EB",
        HardwareTier.High => "#16A34A",
        _ => "#64748B"
    };

    public string TierBackground => Tier switch
    {
        HardwareTier.UltraLow => "#FEE2E2",
        HardwareTier.Low => "#FFEDD5",
        HardwareTier.Entry => "#FEF3C7",
        HardwareTier.Mid => "#EFF6FF",
        HardwareTier.High => "#DCFCE7",
        _ => "#F1F5F9"
    };

    public WindowsLiteProfileType RecommendedProfile { get; set; } = WindowsLiteProfileType.Balanced;
    public string RecommendedProfileName => RecommendedProfile switch
    {
        WindowsLiteProfileType.LowEnd => "Low-End / Potato Mode",
        WindowsLiteProfileType.Light => "Light Mode",
        WindowsLiteProfileType.Balanced => "Balanced Mode",
        WindowsLiteProfileType.Gaming => "Gaming Mode",
        WindowsLiteProfileType.ExtremeGaming => "Extreme Gaming Mode",
        _ => "Balanced Mode"
    };

    public List<string> ClassificationReasons { get; set; } = new();

    // Convenience Compatibility Properties
    public HardwareTier HardwareTier
    {
        get => Tier;
        set => Tier = value;
    }
    public string HardwareTierBadge => TierName;
    public string RecommendationText => RecommendedProfileName;
    public string RecommendationReasonSummary => ClassificationReasons.Count > 0 
        ? string.Join(" • ", ClassificationReasons) 
        : "Spesifikasi seimbang untuk penggunaan harian dan produktivitas.";
    public string SystemDriveType => DriveTypeDisplay;
    public double SystemDriveTotalGb => TotalDiskGb;
}
