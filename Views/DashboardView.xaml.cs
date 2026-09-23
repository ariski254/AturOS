using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AturOS.Models;
using AturOS.Services;

namespace AturOS.Views;

public partial class DashboardView : UserControl
{
    private readonly SystemInfoService _systemInfoService = new();
    private readonly MemoryOptimizerService _memoryService = new();
    private readonly StorageCleanerService _cleanerService = new();
    private readonly PerformanceProfileService _profileService = new();
    private readonly WindowsLiteService _liteService = new();
    private DispatcherTimer? _timer;
    private bool _hasLoadedHwTier = false;

    public DashboardView()
    {
        InitializeComponent();

        Loaded += DashboardView_Loaded;
        Unloaded += DashboardView_Unloaded;
    }

    private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshMetrics();
        LoadHardwareTier();

        var mode = await _profileService.DetectCurrentProfileAsync();
        UpdateProfileRadio(mode);

        if (_timer == null)
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            _timer.Tick += (s, ev) => RefreshMetrics();
        }
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private async void LoadHardwareTier()
    {
        if (_hasLoadedHwTier) return;

        try
        {
            var hw = await System.Threading.Tasks.Task.Run(() => _liteService.GetHardwareEnvironment());
            if (!IsLoaded) return;
            _hasLoadedHwTier = true;
            TxtInfoHwTier.Text = $"TIER: {hw.HardwareTierBadge} • {hw.RecommendationText}";

            switch (hw.HardwareTier)
            {
                case HardwareTier.UltraLow:
                case HardwareTier.Low:
                    BorderHwTierBadge.Background = (System.Windows.Media.Brush)FindResource("BrushWarningLight");
                    TxtInfoHwTier.Foreground = (System.Windows.Media.Brush)FindResource("BrushWarning");
                    break;
                case HardwareTier.Mid:
                case HardwareTier.High:
                    BorderHwTierBadge.Background = (System.Windows.Media.Brush)FindResource("BrushSuccessLight");
                    TxtInfoHwTier.Foreground = (System.Windows.Media.Brush)FindResource("BrushSuccess");
                    break;
                default:
                    BorderHwTierBadge.Background = (System.Windows.Media.Brush)FindResource("BrushAccentLight");
                    TxtInfoHwTier.Foreground = (System.Windows.Media.Brush)FindResource("BrushAccent");
                    break;
            }
        }
        catch
        {
            TxtInfoHwTier.Text = "TIER: DETEKSI SISTEM";
        }
    }

    private void UpdateProfileRadio(PerformanceProfileMode mode)
    {
        RadioDaily.IsChecked = mode == PerformanceProfileMode.DailyBalance;
        RadioWork.IsChecked = mode == PerformanceProfileMode.Productivity;
        RadioGaming.IsChecked = mode == PerformanceProfileMode.Gaming;
    }

    private async void RadioProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;

        PerformanceProfileMode target = tag switch
        {
            "Work" => PerformanceProfileMode.Productivity,
            "Gaming" => PerformanceProfileMode.Gaming,
            _ => PerformanceProfileMode.DailyBalance
        };

        var (success, msg) = await _profileService.ApplyProfileAsync(target);
        ShowBanner(msg, isError: !success);
        RefreshMetrics();
    }

    private void DashboardView_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        _timer = null;
    }

    private void RefreshMetrics()
    {
        var metrics = _systemInfoService.GetMetrics();

        // CPU
        TxtCpuPercent.Text = metrics.CpuUsagePercent.ToString("F0");
        ProgressCpu.Value = metrics.CpuUsagePercent;
        TxtCpuStatus.Text = metrics.CpuUsagePercent > 80 ? "Tinggi" : (metrics.CpuUsagePercent > 40 ? "Sedang" : "Normal");

        // RAM
        TxtRamPercent.Text = metrics.RamUsagePercent.ToString("F0");
        ProgressRam.Value = metrics.RamUsagePercent;
        TxtRamDetails.Text = $"{metrics.UsedRamGb} GB / {metrics.TotalRamGb} GB";
        TxtRamFree.Text = $"{metrics.FreeRamGb} GB";
        TxtRamStatus.Text = metrics.RamUsagePercent > 85 ? "Penuh" : (metrics.RamUsagePercent > 65 ? "Sedang" : "Stabil");

        // Storage
        TxtStoragePercent.Text = metrics.DriveUsagePercent.ToString("F0");
        ProgressStorage.Value = metrics.DriveUsagePercent;
        TxtStorageDetails.Text = $"{metrics.UsedDriveGb} GB / {metrics.TotalDriveGb} GB";
        TxtStorageFree.Text = $"{metrics.FreeDriveGb} GB";
        TxtStorageStatus.Text = metrics.DriveUsagePercent > 90 ? "Kritis" : (metrics.DriveUsagePercent > 75 ? "Waspada" : "Aman");

        // Info
        TxtInfoOs.Text = metrics.OsName;
        TxtInfoBuild.Text = metrics.OsBuild;
        TxtInfoComputer.Text = metrics.ComputerName;
        TxtInfoUser.Text = metrics.UserName;
        TxtInfoArch.Text = metrics.Architecture;
        TxtInfoUptime.Text = metrics.UptimeFormatted;
    }

    private async void BtnOptimizeNow_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Jalankan optimasi cepat sekarang?\n\nOperasi ini akan membebaskan memori kerja (working set) proses yang tidak terpakai secara aman dan membersihkan file sementara pengguna (%TEMP%).",
            "AturOS - Konfirmasi Optimasi",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnOptimizeNow.IsEnabled = false;
        try
        {
            // 1. Memory optimization
            var memResult = await _memoryService.OptimizeRamAsync();

            // 2. Quick clean User Temp
            var targets = _cleanerService.GetDefaultTargets().Where(t => t.Id == "user_temp");
            var cleanResult = await _cleanerService.CleanTargetsAsync(targets);

            RefreshMetrics();

            // Show banner
            ShowBanner(
                $"Optimasi berhasil! Membebaskan sekitar {memResult.FormattedFreed} RAM ({memResult.ProcessedCount} proses di-trim) dan menghapus {cleanResult.FilesDeleted} file sementara ({cleanResult.FormattedFreed}).",
                isError: false);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal menjalankan optimasi: {ex.Message}", isError: true);
        }
        finally
        {
            BtnOptimizeNow.IsEnabled = true;
        }
    }

    private CancellationTokenSource? _bannerCts;

    private async void ShowBanner(string message, bool isError)
    {
        _bannerCts?.Cancel();
        var cts = new CancellationTokenSource();
        _bannerCts = cts;

        BannerText.Text = message;
        NotificationBanner.Background = isError ? (System.Windows.Media.Brush)FindResource("BrushDangerLight") : (System.Windows.Media.Brush)FindResource("BrushSuccessLight");
        NotificationBanner.BorderBrush = isError ? (System.Windows.Media.Brush)FindResource("BrushDanger") : (System.Windows.Media.Brush)FindResource("BrushSuccess");
        BannerIcon.Data = (System.Windows.Media.Geometry)FindResource(isError ? "IconClose" : "IconCheck");
        BannerIcon.Fill = isError ? (System.Windows.Media.Brush)FindResource("BrushDanger") : (System.Windows.Media.Brush)FindResource("BrushSuccess");
        NotificationBanner.Visibility = Visibility.Visible;

        try
        {
            await Task.Delay(isError ? 6000 : 4000, cts.Token);
            NotificationBanner.Visibility = Visibility.Collapsed;
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void BtnCloseBanner_Click(object sender, RoutedEventArgs e)
    {
        _bannerCts?.Cancel();
        NotificationBanner.Visibility = Visibility.Collapsed;
    }
}
