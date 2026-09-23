using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using AturOS.Models;
using AturOS.Services;

namespace AturOS.Views;

public partial class AppDebloaterView : UserControl
{
    private readonly AppDebloaterService _debloaterService = new();
    private List<DebloatAppItem> _allApps = new();
    public ObservableCollection<DebloatAppItem> FilteredApps { get; } = new();
    private bool _isInitialized = false;
    private bool _hasLoadedCatalog = false;
    private bool _isScanning = false;

    public AppDebloaterView()
    {
        InitializeComponent();
        _isInitialized = true;
        ListAppCatalog.ItemsSource = FilteredApps;
        Loaded += AppDebloaterView_Loaded;
    }

    private async void AppDebloaterView_Loaded(object sender, RoutedEventArgs e)
    {
        // Jika katalog sudah pernah dimuat, jangan scan ulang otomatis agar perpindahan menu instan
        if (_hasLoadedCatalog && _allApps.Count > 0)
        {
            return;
        }

        try
        {
            await ReloadCatalogAsync();
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal memuat katalog App Debloater: {ex.Message}");
            ShowBanner($"Gagal memuat katalog: {ex.Message}", isError: true);
        }
    }

    private async Task ReloadCatalogAsync()
    {
        if (_isScanning) return;
        _isScanning = true;

        if (ProgressScanning != null) ProgressScanning.Visibility = Visibility.Visible;
        if (BtnRefresh != null) BtnRefresh.IsEnabled = false;
        if (TxtAppCount != null) TxtAppCount.Text = "Memindai seluruh aplikasi desktop dan paket modern Windows...";

        try
        {
            _allApps = await _debloaterService.ScanInstalledAppsAsync();
            _hasLoadedCatalog = true;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal memuat katalog App Debloater: {ex.Message}");
            ShowBanner($"Gagal memuat katalog aplikasi: {ex.Message}", isError: true);
        }
        finally
        {
            _isScanning = false;
            if (ProgressScanning != null) ProgressScanning.Visibility = Visibility.Collapsed;
            if (BtnRefresh != null) BtnRefresh.IsEnabled = true;
        }
    }

    private void ApplyFilter()
    {
        if (!_isInitialized || TxtSearch == null || ComboCategory == null || CheckSafeOnly == null || TxtAppCount == null)
            return;

        var search = TxtSearch.Text?.Trim().ToLowerInvariant() ?? "";
        var selectedCat = (ComboCategory.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Semua Aplikasi (Sistem & Terinstall)";
        bool safeOnly = CheckSafeOnly.IsChecked == true;

        FilteredApps.Clear();
        foreach (var app in _allApps)
        {
            if (safeOnly && !app.IsSafeToRemove) continue;

            if (selectedCat == "Hanya Aplikasi Sistem Windows")
            {
                if (!app.IsSystemApp) continue;
            }
            else if (selectedCat == "Hanya Aplikasi Pengguna")
            {
                if (app.IsSystemApp) continue;
            }
            else if (selectedCat == "Aplikasi Desktop (Win32)")
            {
                if (app.AppType != AppInstallType.Win32) continue;
            }
            else if (selectedCat == "Aplikasi Modern (UWP / Store)")
            {
                if (app.AppType != AppInstallType.Uwp) continue;
            }
            else if (selectedCat == "Gaming & Hiburan")
            {
                if (!app.Category.Equals("Gaming & Hiburan", StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (!string.IsNullOrEmpty(search))
            {
                bool matches = app.DisplayName.ToLowerInvariant().Contains(search) ||
                                (!string.IsNullOrEmpty(app.Publisher) && app.Publisher.ToLowerInvariant().Contains(search)) ||
                                (!string.IsNullOrEmpty(app.PackageName) && app.PackageName.ToLowerInvariant().Contains(search));

                if (!matches) continue;
            }

            FilteredApps.Add(app);
        }

        TxtAppCount.Text = $"Menampilkan {FilteredApps.Count} dari {_allApps.Count} aplikasi (Sistem & Terinstall)";
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (BtnRefresh != null) BtnRefresh.IsEnabled = false;
        try
        {
            await ReloadCatalogAsync();
            ShowBanner($"Katalog aplikasi berhasil diperbarui. Total {_allApps.Count} aplikasi ditemukan.", isError: false);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal menyegarkan katalog aplikasi: {ex.Message}");
            ShowBanner($"Gagal menyegarkan katalog: {ex.Message}", isError: true);
        }
        finally
        {
            if (BtnRefresh != null) BtnRefresh.IsEnabled = true;
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TxtSearchPlaceholder != null)
        {
            TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
        ApplyFilter();
    }

    private void ComboCategory_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();
    private void CheckSafeOnly_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

    private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DebloatAppItem app) return;

        btn.IsEnabled = false;
        try
        {
            ShowBanner($"Menganalisis komponen '{app.DisplayName}'...", isError: false);
            var discovery = await PermanentUninstallEngine.DiscoverComponentsAsync(app);

            var prompt = new System.Text.StringBuilder();
            prompt.AppendLine("KONFIRMASI PENGHAPUSAN PERMANEN (PERMANENT UNINSTALL ENGINE)");
            prompt.AppendLine("============================================================");
            prompt.AppendLine($"Aplikasi    : {app.DisplayName}");
            prompt.AppendLine($"Publisher   : {(string.IsNullOrWhiteSpace(app.Publisher) ? "-" : app.Publisher)}");
            prompt.AppendLine($"Versi       : {(string.IsNullOrWhiteSpace(app.Version) ? "-" : app.Version)}");
            prompt.AppendLine($"Tipe        : {app.AppTypeDisplay}");
            if (!string.IsNullOrWhiteSpace(app.InstallLocation))
            {
                prompt.AppendLine($"Lokasi      : {app.InstallLocation}");
            }
            prompt.AppendLine();
            prompt.AppendLine("Komponen terdeteksi yang akan dibersihkan tuntas:");
            prompt.AppendLine($"• Direktori Instalasi & Residu ({discovery.ResidualDirectories.Count} folder)");
            if (discovery.Processes.Count > 0)
                prompt.AppendLine($"• Proses Berjalan ({discovery.Processes.Count} proses aktif)");
            if (discovery.Services.Count > 0)
                prompt.AppendLine($"• Layanan Sistem Windows ({discovery.Services.Count} service terdaftar)");
            if (discovery.ScheduledTasks.Count > 0)
                prompt.AppendLine($"• Scheduled Tasks ({discovery.ScheduledTasks.Count} task terjadwal)");
            if (discovery.StartupEntries.Count > 0)
                prompt.AppendLine($"• Entri Startup / Autorun ({discovery.StartupEntries.Count} entri)");
            if (discovery.ShortcutFiles.Count > 0)
                prompt.AppendLine($"• Shortcut Desktop & Start Menu ({discovery.ShortcutFiles.Count} berkas .lnk)");
            prompt.AppendLine("• Registrasi Registry Uninstall & Shell");
            prompt.AppendLine();
            prompt.AppendLine("Perlindungan Integritas Sistem & Pengguna:");
            prompt.AppendLine("✓ Data Pribadi Pengguna (Documents, Desktop, Pictures): AMAN & DIPERTAHANKAN");
            prompt.AppendLine("✓ Dependensi Sistem (Windows, System32, WinSxS): AMAN & DIPERTAHANKAN");
            prompt.AppendLine();
            prompt.AppendLine("Lanjutkan eksekusi pencopotan permanen?");

            var confirm = MessageBox.Show(
                prompt.ToString(),
                $"Konfirmasi Uninstall Permanen: {app.DisplayName}",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                ShowBanner($"Uninstall '{app.DisplayName}' dibatalkan oleh pengguna.", isError: false);
                return;
            }

            ShowBanner($"Sedang menguninstall '{app.DisplayName}'...", isError: false);
            var (success, msg) = await _debloaterService.UninstallAppAsync(app, progressMsg =>
            {
                Dispatcher.Invoke(() => ShowBanner(progressMsg, isError: false));
            });

            ShowBanner(msg, isError: !success);
            if (success)
            {
                _allApps.Remove(app);
            }
            ApplyFilter();
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Terjadi kesalahan saat menguninstall '{app.DisplayName}': {ex.Message}");
            ShowBanner($"Gagal menguninstall '{app.DisplayName}': {ex.Message}", isError: true);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private void BtnReinstallStore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DebloatAppItem app) return;
        try
        {
            AppDebloaterService.OpenStoreForReinstall(app);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal membuka Microsoft Store: {ex.Message}");
            ShowBanner("Gagal membuka Microsoft Store pada sistem ini.", isError: true);
        }
    }

    private CancellationTokenSource? _bannerCts;

    private async void ShowBanner(string message, bool isError)
    {
        _bannerCts?.Cancel();
        var cts = new CancellationTokenSource();
        _bannerCts = cts;

        TxtBanner.Text = message;
        StatusBanner.Background = isError ? (System.Windows.Media.Brush)FindResource("BrushDangerLight") : (System.Windows.Media.Brush)FindResource("BrushSuccessLight");
        StatusBanner.BorderBrush = isError ? (System.Windows.Media.Brush)FindResource("BrushDanger") : (System.Windows.Media.Brush)FindResource("BrushSuccess");
        StatusBanner.Visibility = Visibility.Visible;

        try
        {
            await Task.Delay(isError ? 6000 : 4000, cts.Token);
            StatusBanner.Visibility = Visibility.Collapsed;
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void BtnCloseBanner_Click(object sender, RoutedEventArgs e)
    {
        _bannerCts?.Cancel();
        StatusBanner.Visibility = Visibility.Collapsed;
    }
}
