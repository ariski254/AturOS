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
        try
        {
            RefreshRamMetrics();
            await RefreshTopProcessesAsync();
            await RefreshRamTweaksStatusAsync();
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Inisialisasi tampilan MemoryOptimizerView gagal: {ex.Message}");
        }
    }

    private void RefreshRamMetrics()
    {
        try
        {
            var metrics = _systemInfoService.GetMetrics();
            TxtTotalRam.Text = $"{metrics.TotalRamGb} GB";
            TxtUsedRam.Text = $"{metrics.UsedRamGb} GB ({metrics.RamUsagePercent}%)";
            TxtFreeRam.Text = $"{metrics.FreeRamGb} GB";
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal memperbarui metrik RAM: {ex.Message}");
        }
    }

    private async Task RefreshTopProcessesAsync()
    {
        try
        {
            var top = await _memoryService.GetTopProcessesAsync(15);
            if (IsLoaded)
            {
                GridTopProcesses.ItemsSource = top;
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal memperbarui daftar proses: {ex.Message}");
        }
    }

    private async Task RefreshRamTweaksStatusAsync()
    {
        try
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
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal memperbarui status tweak RAM: {ex.Message}");
        }
    }

    private void UpdateBadge(Border badge, TextBlock textBlock, string text, bool isPositive)
    {
        try
        {
            textBlock.Text = text;
            var bgBrush = (TryFindResource(isPositive ? "BrushSuccessLight" : "BrushBackground") as System.Windows.Media.Brush)
                          ?? (isPositive ? System.Windows.Media.Brushes.Honeydew : System.Windows.Media.Brushes.WhiteSmoke);
            var fgBrush = (TryFindResource(isPositive ? "BrushSuccess" : "BrushTextSecondary") as System.Windows.Media.Brush)
                          ?? (isPositive ? System.Windows.Media.Brushes.ForestGreen : System.Windows.Media.Brushes.DimGray);

            badge.Background = bgBrush;
            textBlock.Foreground = fgBrush;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"UpdateBadge error: {ex.Message}");
        }
    }

    private async void BtnRefreshProcesses_Click(object sender, RoutedEventArgs e)
    {
        BtnRefreshProcesses.IsEnabled = false;
        try
        {
            RefreshRamMetrics();
            await RefreshTopProcessesAsync();
            await RefreshRamTweaksStatusAsync();
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal menyegarkan: {ex.Message}", isError: true);
        }
        finally
        {
            BtnRefreshProcesses.IsEnabled = true;
        }
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
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _memoryService.SetDisablePagingExecutiveAsync(true);
            await RefreshRamTweaksStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Terjadi kesalahan: {ex.Message}", isError: true);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnPagingExecOff_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _memoryService.SetDisablePagingExecutiveAsync(false);
            await RefreshRamTweaksStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Terjadi kesalahan: {ex.Message}", isError: true);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnClearPagefileOn_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _memoryService.SetClearPageFileAtShutdownAsync(true);
            await RefreshRamTweaksStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Terjadi kesalahan: {ex.Message}", isError: true);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnClearPagefileOff_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _memoryService.SetClearPageFileAtShutdownAsync(false);
            await RefreshRamTweaksStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Terjadi kesalahan: {ex.Message}", isError: true);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnLargeCacheOn_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _memoryService.SetLargeSystemCacheAsync(true);
            await RefreshRamTweaksStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Terjadi kesalahan: {ex.Message}", isError: true);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnLargeCacheOff_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _memoryService.SetLargeSystemCacheAsync(false);
            await RefreshRamTweaksStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Terjadi kesalahan: {ex.Message}", isError: true);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnCompressionOn_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _memoryService.SetMemoryCompressionAsync(true);
            await RefreshRamTweaksStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Terjadi kesalahan: {ex.Message}", isError: true);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnCompressionOff_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _memoryService.SetMemoryCompressionAsync(false);
            await RefreshRamTweaksStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Terjadi kesalahan: {ex.Message}", isError: true);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnSysMainOn_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _memoryService.SetSysMainStatusAsync(true);
            await RefreshRamTweaksStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Terjadi kesalahan: {ex.Message}", isError: true);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnSysMainOff_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _memoryService.SetSysMainStatusAsync(false);
            await RefreshRamTweaksStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Terjadi kesalahan: {ex.Message}", isError: true);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private CancellationTokenSource? _bannerCts;

    private async void ShowBanner(string message, bool isError)
    {
        try
        {
            _bannerCts?.Cancel();
            var cts = new CancellationTokenSource();
            _bannerCts = cts;

            TxtBanner.Text = message;

            var bgBrush = (TryFindResource(isError ? "BrushDangerLight" : "BrushSuccessLight") as System.Windows.Media.Brush)
                          ?? (isError ? System.Windows.Media.Brushes.MistyRose : System.Windows.Media.Brushes.Honeydew);
            var borderBrush = (TryFindResource(isError ? "BrushDanger" : "BrushSuccess") as System.Windows.Media.Brush)
                              ?? (isError ? System.Windows.Media.Brushes.Crimson : System.Windows.Media.Brushes.ForestGreen);

            StatusBanner.Background = bgBrush;
            StatusBanner.BorderBrush = borderBrush;
            StatusBanner.Visibility = Visibility.Visible;

            await Task.Delay(isError ? 6000 : 4000, cts.Token);
            StatusBanner.Visibility = Visibility.Collapsed;
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Banner error: {ex.Message}");
        }
    }

    private void BtnCloseBanner_Click(object sender, RoutedEventArgs e)
    {
        _bannerCts?.Cancel();
        StatusBanner.Visibility = Visibility.Collapsed;
    }
}
