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

    public AppDebloaterView()
    {
        InitializeComponent();
        _isInitialized = true;
        ListAppCatalog.ItemsSource = FilteredApps;
        Loaded += AppDebloaterView_Loaded;
    }

    private async void AppDebloaterView_Loaded(object sender, RoutedEventArgs e)
    {
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
        if (ProgressScanning != null) ProgressScanning.Visibility = Visibility.Visible;
        if (BtnRefresh != null) BtnRefresh.IsEnabled = false;
        if (TxtAppCount != null) TxtAppCount.Text = "Memindai seluruh aplikasi desktop dan paket modern Windows...";

        try
        {
            _allApps = await _debloaterService.ScanInstalledAppsAsync();
            ApplyFilter();
        }
        finally
        {
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
        await ReloadCatalogAsync();
        ShowBanner($"Katalog aplikasi berhasil diperbarui. Total {_allApps.Count} aplikasi ditemukan.", isError: false);
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

        string prompt = app.AppType == AppInstallType.Uwp
            ? $"Copot pemasangan paket '{app.DisplayName}' dari sistem Windows?\n\nAplikasi ini dapat dipasang kembali melalui Microsoft Store kapan saja."
            : $"Jalankan uninstaller untuk '{app.DisplayName}'?\n\nJendela dialog pencopotan aplikasi resmi akan dijalankan.";

        var confirm = MessageBox.Show(
            prompt,
            $"Konfirmasi Copot {app.DisplayName}",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        ShowBanner($"Sedang mencopot '{app.DisplayName}'...", isError: false);
        var (success, msg) = await _debloaterService.UninstallAppAsync(app);
        ShowBanner(msg, isError: !success);
        ApplyFilter();
    }

    private void BtnReinstallStore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DebloatAppItem app) return;
        AppDebloaterService.OpenStoreForReinstall(app);
    }

    private void ShowBanner(string message, bool isError)
    {
        TxtBanner.Text = message;
        StatusBanner.Background = isError ? (System.Windows.Media.Brush)FindResource("BrushDangerLight") : (System.Windows.Media.Brush)FindResource("BrushSuccessLight");
        StatusBanner.BorderBrush = isError ? (System.Windows.Media.Brush)FindResource("BrushDanger") : (System.Windows.Media.Brush)FindResource("BrushSuccess");
        StatusBanner.Visibility = Visibility.Visible;
    }

    private void BtnCloseBanner_Click(object sender, RoutedEventArgs e)
    {
        StatusBanner.Visibility = Visibility.Collapsed;
    }
}
