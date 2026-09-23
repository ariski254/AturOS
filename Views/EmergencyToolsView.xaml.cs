using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AturOS.Services;

namespace AturOS.Views;

public partial class EmergencyToolsView : UserControl
{
    private readonly EmergencyToolsService _emergencyService = new();

    public EmergencyToolsView()
    {
        InitializeComponent();
    }

    private async void BtnKillFrozen_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _emergencyService.KillNotRespondingTasksAsync();
            ShowBanner(m, isError: !s);
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

    private async void BtnRestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var ok = await _emergencyService.RestartExplorerAsync();
            ShowBanner(ok ? "Windows Explorer berhasil direstart." : "Gagal merestart Windows Explorer.", isError: !ok);
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

    private async void BtnClearClipboard_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var ok = await _emergencyService.ClearClipboardAsync();
            ShowBanner(ok ? "Riwayat clipboard sistem telah dikosongkan." : "Gagal mengosongkan clipboard.", isError: !ok);
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

    private async void BtnBatteryReport_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, p) = await _emergencyService.GenerateBatteryReportAsync();
            ShowBanner(s ? $"Laporan baterai berhasil dibuka di browser: {p}" : p, isError: !s);
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

    private async void BtnSaveLidAction_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var tag = (ComboLidAction.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "0";
            int.TryParse(tag, out int val);
            var (s, m) = await _emergencyService.SetLidCloseActionAsync((LidCloseAction)val);
            ShowBanner(m, isError: !s);
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

    private async void BtnSafeModeOn_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Setel komputer untuk masuk ke Safe Mode pada restart berikutnya?",
            "Konfirmasi Safe Mode",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _emergencyService.SetSafeModeBootAsync(true);
            ShowBanner(m, isError: !s);
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

    private async void BtnSafeModeOff_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _emergencyService.SetSafeModeBootAsync(false);
            ShowBanner(m, isError: !s);
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

    private async void BtnCleanGhostDevices_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Bersihkan entri driver hantu/perangkat terputus (Ghost Devices)?\n\nOperasi ini akan menghapus driver perangkat nonaktif (seperti USB/Bluetooth lama) dari Device Manager secara aman.",
            "Konfirmasi Pembersih Ghost Devices",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnGhostDevices.IsEnabled = false;
        SetLoading(true, "Memindai dan menghapus perangkat terputus lama...");
        try
        {
            var (s, m, count) = await _emergencyService.CleanGhostDevicesAsync(p => Dispatcher.BeginInvoke(() => TxtProgress.Text = p));
            ShowBanner(m, isError: !s);
        }
        finally
        {
            BtnGhostDevices.IsEnabled = true;
            SetLoading(false);
        }
    }

    private async void BtnRunSfc_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Mulai pemindaian file sistem (sfc /scannow)?\n\nOperasi ini membutuhkan hak Administrator dan dapat memakan waktu 5-10 menit.",
            "Konfirmasi SFC Scannow",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        SetLoading(true, "Menjalankan SFC Scannow (mohon tunggu)...");
        try
        {
            var (s, m) = await _emergencyService.RunSfcScannowAsync(p => Dispatcher.BeginInvoke(() => TxtProgress.Text = p));
            ShowBanner(m, isError: !s);
        }
        finally
        {
            SetLoading(false);
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnRunDism_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Mulai pemulihan citra sistem (DISM RestoreHealth)?\n\nOperasi ini membutuhkan hak Administrator dan koneksi internet stabil.",
            "Konfirmasi DISM RestoreHealth",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        SetLoading(true, "Menjalankan DISM RestoreHealth (mohon tunggu)...");
        try
        {
            var (s, m) = await _emergencyService.RunDismRestoreHealthAsync(p => Dispatcher.BeginInvoke(() => TxtProgress.Text = p));
            ShowBanner(m, isError: !s);
        }
        finally
        {
            SetLoading(false);
            if (btn != null) btn.IsEnabled = true;
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
