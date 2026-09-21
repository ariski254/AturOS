using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AturOS.Models;

public enum AppInstallType
{
    Uwp,
    Win32
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
    public string UninstallString { get; set; } = string.Empty;
    public string QuietUninstallString { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public bool IsSafeToRemove { get; set; } = true;
    public bool IsSystemApp { get; set; } = false;
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

    public string OriginBadgeText => IsSystemApp ? "Sistem Windows" : "Aplikasi Pengguna";

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
