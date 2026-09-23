using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AturOS.Models;

public enum AppInstallType
{
    Uwp,
    Win32
}

public enum AppClassification
{
    Essential,
    System,
    Security,
    Driver,
    Dependency,
    RecommendedRemove,
    Optional,
    UserApp,
    OemBloat,
    Unknown
}

public class DebloatAppItem : INotifyPropertyChanged
{
    private bool _isInstalled = true;
    private bool _isProcessing;

    public string PackageName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Aplikasi Desktop (Win32)";
    public AppInstallType AppType { get; set; } = AppInstallType.Uwp;
    public AppClassification Classification { get; set; } = AppClassification.Unknown;
    public string UninstallString { get; set; } = string.Empty;
    public string QuietUninstallString { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public bool IsSafeToRemove { get; set; } = true;
    public bool IsSystemApp { get; set; } = false;
    public bool IsNonRemovable { get; set; } = false;
    public string StoreUrl { get; set; } = string.Empty;

    public bool IsInstalled
    {
        get => _isInstalled;
        set { _isInstalled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set { _isProcessing = value; OnPropertyChanged(); }
    }

    public string StatusDisplay => IsInstalled ? "Terpasang" : "Dicopot";

    public string AppTypeDisplay => AppType == AppInstallType.Win32 ? "Win32 Desktop" : "UWP / Store";

    public string OriginBadgeText => Classification switch
    {
        AppClassification.OemBloat => "OEM Bloatware",
        AppClassification.RecommendedRemove => "Disarankan Copot",
        AppClassification.Essential => "Inti Sistem (Wajib)",
        AppClassification.Security => "Keamanan Windows",
        AppClassification.Driver => "Driver Perangkat",
        AppClassification.Dependency => "Dependensi Sistem",
        AppClassification.System => "Sistem Windows",
        AppClassification.Optional => "Opsional",
        AppClassification.UserApp => "Aplikasi Pengguna",
        _ => IsSystemApp ? "Sistem Windows" : "Aplikasi Pengguna"
    };

    public string ClassificationForeground => Classification switch
    {
        AppClassification.OemBloat => "#DC2626",
        AppClassification.RecommendedRemove => "#DC2626",
        AppClassification.Essential => "#2563EB",
        AppClassification.Security => "#16A34A",
        AppClassification.Driver => "#0F172A",
        AppClassification.UserApp => "#2563EB",
        _ => "#64748B"
    };

    public string ClassificationBackground => Classification switch
    {
        AppClassification.OemBloat => "#FEE2E2",
        AppClassification.RecommendedRemove => "#FEE2E2",
        AppClassification.Essential => "#EFF6FF",
        AppClassification.Security => "#DCFCE7",
        AppClassification.Driver => "#F1F5F9",
        AppClassification.UserApp => "#EFF6FF",
        _ => "#F1F5F9"
    };

    public bool HasStoreLink => AppType == AppInstallType.Uwp && !string.IsNullOrEmpty(StoreUrl);

    public string ActionSecondaryText => AppType == AppInstallType.Uwp ? "Buka Store" : "Buka Folder";

    public bool CanActionSecondary => AppType == AppInstallType.Uwp || !string.IsNullOrWhiteSpace(InstallLocation);

    public string FormattedDetails
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Publisher)) parts.Add(Publisher);
            if (!string.IsNullOrWhiteSpace(Version)) parts.Add($"v{Version}");
            return parts.Count > 0 ? string.Join(" • ", parts) : Description;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
