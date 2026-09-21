using System.Windows;
using System.Windows.Controls;
using AturOS.Services;

namespace AturOS.Views;

public partial class MemoryOptimizerView : UserControl
{
    private readonly MemoryOptimizerService _memoryService = new();
    private readonly SystemInfoService _systemInfoService = new();

    public MemoryOptimizerView()
    {
        InitializeComponent();
        Loaded += MemoryOptimizerView_Loaded;
    }

    private async void MemoryOptimizerView_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshRamMetrics();
        await RefreshTopProcessesAsync();
        await RefreshRamTweaksStatusAsync();
    }

    private void RefreshRamMetrics()
    {
        var metrics = _systemInfoService.GetMetrics();
        TxtTotalRam.Text = $"{metrics.TotalRamGb} GB";
        TxtUsedRam.Text = $"{metrics.UsedRamGb} GB ({metrics.RamUsagePercent}%)";
        TxtFreeRam.Text = $"{metrics.FreeRamGb} GB";
    }

    private async Task RefreshTopProcessesAsync()
    {
        var top = await _memoryService.GetTopProcessesAsync(15);
        GridTopProcesses.ItemsSource = top;
    }

    private async Task RefreshRamTweaksStatusAsync()
    {
        // 1. Paging Executive
        bool pagingOn = _memoryService.GetDisablePagingExecutiveStatus();
        UpdateBadge(BadgePagingExec, TxtBadgePagingExec, pagingOn ? "Aktif (Lock RAM)" : "Standar", pagingOn);

        // 2. Clear Pagefile at Shutdown
        bool clearPagefileOn = _memoryService.GetClearPageFileAtShutdownStatus();
        UpdateBadge(BadgeClearPagefile, TxtBadgeClearPagefile, clearPagefileOn ? "Aktif" : "Nonaktif", clearPagefileOn);

        // 3. Large System Cache
        bool largeCacheOn = _memoryService.GetLargeSystemCacheStatus();
        UpdateBadge(BadgeLargeCache, TxtBadgeLargeCache, largeCacheOn ? "Aktif" : "Default", largeCacheOn);

        // 4. Memory Compression
        var compStatus = await _memoryService.GetMemoryCompressionStatusAsync();
        if (compStatus.HasValue)
        {
            UpdateBadge(BadgeCompression, TxtBadgeCompression, compStatus.Value ? "Aktif" : "Nonaktif", compStatus.Value);
        }
        else
        {
            UpdateBadge(BadgeCompression, TxtBadgeCompression, "Tidak Didukung", false);
        }

        // 5. SysMain
        bool sysMainRunning = _memoryService.GetSysMainStatus();
        UpdateBadge(BadgeSysMain, TxtBadgeSysMain, sysMainRunning ? "Berjalan" : "Berhenti/Mati", sysMainRunning);
    }

    private void UpdateBadge(Border badge, TextBlock textBlock, string text, bool isPositive)
    {
        textBlock.Text = text;
        badge.Background = isPositive
            ? (System.Windows.Media.Brush)FindResource("BrushSuccessLight")
            : (System.Windows.Media.Brush)FindResource("BrushBackground");
        textBlock.Foreground = isPositive
            ? (System.Windows.Media.Brush)FindResource("BrushSuccess")
            : (System.Windows.Media.Brush)FindResource("BrushTextSecondary");
    }

    private async void BtnRefreshProcesses_Click(object sender, RoutedEventArgs e)
    {
        BtnRefreshProcesses.IsEnabled = false;
        RefreshRamMetrics();
        await RefreshTopProcessesAsync();
        await RefreshRamTweaksStatusAsync();
        BtnRefreshProcesses.IsEnabled = true;
    }

    private async void BtnOptimizeRam_Click(object sender, RoutedEventArgs e)
    {
        BtnOptimizeRam.IsEnabled = false;

        try
        {
            var result = await _memoryService.OptimizeRamAsync();

            RefreshRamMetrics();
            await RefreshTopProcessesAsync();

            ShowBanner(
                $"Optimasi RAM Selesai! Berhasil memangkas working set {result.ProcessedCount} proses ({result.SkippedCount} proses sistem/terproteksi dilewati). Estimasi memori dibebaskan: {result.FormattedFreed}.",
                isError: false);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal melakukan optimasi RAM: {ex.Message}", isError: true);
        }
        finally
        {
            BtnOptimizeRam.IsEnabled = true;
        }
    }

    private async void BtnFlushStandby_Click(object sender, RoutedEventArgs e)
    {
        BtnFlushStandby.IsEnabled = false;

        try
        {
            var result = await _memoryService.FlushStandbyListAsync();

            RefreshRamMetrics();
            await RefreshTopProcessesAsync();

            ShowBanner(
                $"Standby List & Cache Berhasil Dibersihkan! Estimasi RAM dibebaskan: {result.FormattedFreed}.",
                isError: false);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal membersihkan Standby List: {ex.Message}", isError: true);
        }
        finally
        {
            BtnFlushStandby.IsEnabled = true;
        }
    }

    private async void BtnPagingExecOn_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _memoryService.SetDisablePagingExecutiveAsync(true);
        await RefreshRamTweaksStatusAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnPagingExecOff_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _memoryService.SetDisablePagingExecutiveAsync(false);
        await RefreshRamTweaksStatusAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnClearPagefileOn_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _memoryService.SetClearPageFileAtShutdownAsync(true);
        await RefreshRamTweaksStatusAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnClearPagefileOff_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _memoryService.SetClearPageFileAtShutdownAsync(false);
        await RefreshRamTweaksStatusAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnLargeCacheOn_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _memoryService.SetLargeSystemCacheAsync(true);
        await RefreshRamTweaksStatusAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnLargeCacheOff_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _memoryService.SetLargeSystemCacheAsync(false);
        await RefreshRamTweaksStatusAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnCompressionOn_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _memoryService.SetMemoryCompressionAsync(true);
        await RefreshRamTweaksStatusAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnCompressionOff_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _memoryService.SetMemoryCompressionAsync(false);
        await RefreshRamTweaksStatusAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnSysMainOn_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _memoryService.SetSysMainStatusAsync(true);
        await RefreshRamTweaksStatusAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnSysMainOff_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _memoryService.SetSysMainStatusAsync(false);
        await RefreshRamTweaksStatusAsync();
        ShowBanner(msg, isError: !success);
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
