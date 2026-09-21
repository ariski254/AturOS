using System.Windows;
using System.Windows.Controls;
using AturOS.Helpers;
using AturOS.Services;

namespace AturOS.Views;

public partial class WindowsLiteView : UserControl
{
    private readonly WindowsLiteService _liteService = new();

    public WindowsLiteView()
    {
        InitializeComponent();
    }

    private async void BtnApplyLevel1_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Terapkan Windows Lite Tingkat 1 (Safe Lite)?\n\nBloatware promosi pihak ketiga akan dicopot, telemetri dasar dan Widgets dimatikan.",
            "Konfirmasi Safe Lite",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        SetLoading(true, "Menerapkan Safe Lite...");
        try
        {
            var (success, msg) = await _liteService.ApplyLiteLevel1Async(p => Dispatcher.Invoke(() => TxtProgress.Text = p));
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal menerapkan Safe Lite: {ex.Message}", isError: true);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void BtnApplyLevel2_Click(object sender, RoutedEventArgs e)
    {
        if (!AdministratorHelper.IsAdministrator)
        {
            MessageBox.Show("Menerapkan Tingkat 2 memerlukan hak Administrator untuk mematikan service SysMain dan WSearch.", "Hak Akses Diperlukan", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            "Terapkan Windows Lite Tingkat 2 (Advanced Lite)?\n\nAplikasi bawaan non-esensial akan dicopot, Copilot dimatikan, service SysMain & Search Indexer dihentikan, dan transparansi dinonaktifkan.",
            "Konfirmasi Advanced Lite",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        SetLoading(true, "Menerapkan Advanced Lite...");
        try
        {
            var (success, msg) = await _liteService.ApplyLiteLevel2Async(p => Dispatcher.Invoke(() => TxtProgress.Text = p));
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal menerapkan Advanced Lite: {ex.Message}", isError: true);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void BtnApplyLevel3_Click(object sender, RoutedEventArgs e)
    {
        if (!AdministratorHelper.IsAdministrator)
        {
            MessageBox.Show("Menerapkan Tingkat 3 memerlukan hak Administrator.", "Hak Akses Diperlukan", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            "PERINGATAN: Terapkan Windows Lite Tingkat 3 (Extreme Barebone)?\n\nVisual akan dialihkan ke mode performa murni, hibernasi dimatikan, kompresi CompactOS dijalankan, dan Print Spooler dihentikan.",
            "Konfirmasi Extreme Lite",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        SetLoading(true, "Menerapkan Extreme Lite (proses ini memerlukan beberapa menit)...");
        try
        {
            var (success, msg) = await _liteService.ApplyLiteLevel3Async(p => Dispatcher.Invoke(() => TxtProgress.Text = p));
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal menerapkan Extreme Lite: {ex.Message}", isError: true);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void BtnRevert_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Kembalikan seluruh konfigurasi ke Standar Windows (Revert)?\n\nLayanan sistem (SysMain, WSearch, Spooler, DiagTrack) akan dihidupkan kembali, animasi dan efek transparansi dipulihkan.",
            "Konfirmasi Revert Standar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        SetLoading(true, "Mengembalikan konfigurasi sistem ke standar...");
        try
        {
            var (success, msg) = await _liteService.RevertToStandardAsync(p => Dispatcher.Invoke(() => TxtProgress.Text = p));
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal mengembalikan pengaturan: {ex.Message}", isError: true);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void BtnRestoreStore_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Jalankan instalasi pemulihan Microsoft Store?\n\nWindows akan mendaftarkan ulang dan mereset komponen Store.",
            "Pulihkan Microsoft Store",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        SetLoading(true, "Memulihkan paket Microsoft Store...");
        try
        {
            var (success, msg) = await WindowsLiteService.RestoreMicrosoftStoreAsync(p => Dispatcher.Invoke(() => TxtProgress.Text = p));
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal memulihkan Store: {ex.Message}", isError: true);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool isLoading, string text = "")
    {
        ProgressCard.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        if (isLoading) TxtProgress.Text = text;
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
