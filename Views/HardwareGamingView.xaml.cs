using System.Windows;
using System.Windows.Controls;
using AturOS.Helpers;
using AturOS.Services;

namespace AturOS.Views;

public partial class HardwareGamingView : UserControl
{
    private readonly HardwareTuningService _tuningService = new();

    public HardwareGamingView()
    {
        InitializeComponent();
    }

    private async void BtnCleanShader_Click(object sender, RoutedEventArgs e)
    {
        var (freed, count) = await _tuningService.CleanShaderCacheAsync();
        string sizeText = freed >= 1024L * 1024
            ? $"{(double)freed / (1024 * 1024):F1} MB"
            : $"{freed / 1024} KB";

        ShowBanner($"Pembersihan Shader Cache selesai: {count} file dihapus ({sizeText} dibebaskan).", isError: false);
    }

    private async void BtnHagsOn_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetHagsAsync(true);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnHagsOff_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetHagsAsync(false);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnTweakGpu_Click(object sender, RoutedEventArgs e)
    {
        await _tuningService.DisableGameDvrAsync(true);
        var (s2, m2) = await _tuningService.OptimizeTdrDelayAsync();
        ShowBanner($"Xbox Game DVR dimatikan. {m2}", isError: false);
    }

    private async void BtnDisableCoreParking_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.DisableCoreParkingAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnDisableDynamicTick_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.DisableDynamicTickAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnDisableThrottling_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.DisablePowerThrottlingAsync(true);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnPagingExecOn_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetDisablePagingExecutiveAsync(true);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnPagingExecOff_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetDisablePagingExecutiveAsync(false);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnTcpNoDelay_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.DisableNagleAlgorithmAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnEnableTrim_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.EnableSsdTrimAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnDisableNtfs83_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.DisableNtfs8Dot3Async();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.FlushDnsAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnDisableMouseAccel_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.DisableMouseAccelerationAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnMaxKeyboard_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.MaximizeKeyboardResponseAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnDisableStickyKeys_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.DisableStickyKeysPopupAsync();
        ShowBanner(msg, isError: !success);
    }

    private async void BtnGpuPriorityOn_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetGpuPrioritySchedulingAsync(true);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnGpuPriorityOff_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetGpuPrioritySchedulingAsync(false);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnPowerUltimate_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetUltimatePerformanceAsync(true);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnPowerBalanced_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetUltimatePerformanceAsync(false);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnCpuPriorityOn_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetWin32PrioritySeparationAsync(true);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnCpuPriorityOff_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetWin32PrioritySeparationAsync(false);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnNetworkThrottlingOff_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetNetworkThrottlingAsync(true);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnNetworkThrottlingOn_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetNetworkThrottlingAsync(false);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnMenuDelayZero_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetMenuShowDelayAsync(true);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnMenuDelayDefault_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _tuningService.SetMenuShowDelayAsync(false);
        ShowBanner(msg, isError: !success);
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
