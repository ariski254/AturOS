using System;

namespace AturOS.Models;

public class RuntimeDependencyItem
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }
    public string Version { get; set; } = string.Empty;
    public string WingetId { get; set; } = string.Empty;

    public string StatusText => IsInstalled ? $"Terpasang ({Version})" : "Belum Terpasang";
    public string StatusBadgeColor => IsInstalled ? "#10B981" : "#EF4444";
    public string StatusBadgeBg => IsInstalled ? "#DCFCE7" : "#FEE2E2";
}

public class BrokenShortcutItem
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string BrokenTarget { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = true;
}

public class OrphanedRegistryItem
{
    public string DisplayName { get; set; } = string.Empty;
    public string KeyPath { get; set; } = string.Empty;
    public string InvalidPath { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = true;
}

public class DiskHealthReport
{
    public string DriveLetter { get; set; } = "C:";
    public string VolumeLabel { get; set; } = string.Empty;
    public string FileSystem { get; set; } = "NTFS";
    public double TotalSpaceGb { get; set; }
    public double FreeSpaceGb { get; set; }
    public double UsedPercent { get; set; }
    public bool IsDirty { get; set; }
    public string DirtyStatusText => IsDirty 
        ? "Corrupt / Dirty Bit Aktif (Perlu chkdsk)" 
        : "Bersih / Normal (Tidak ada error integritas)";
    public bool ChkdskScheduled { get; set; }
    public string OverallStatus => IsDirty ? "Membutuhkan Perbaikan" : "Sehat";
}
