using System.Windows;
using System.Windows.Controls;
using AturOS.Services;

namespace AturOS.Views;

public partial class WindowsUpdateView : UserControl
{
    private readonly WindowsUpdateControlService _updateService = new();

    public WindowsUpdateView()
    {
        InitializeComponent();
    }

    private async void BtnHardLockdown_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Aktifkan Hard Lockdown pada Windows Update?\n\nSemua layanan pembaruan otomatis Windows akan dimatikan dan dikunci. Anda dapat mengembalikannya kapan saja melalui tombol 'Kembalikan ke Default'.",
            "Konfirmasi Hard Lockdown",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _updateService.ApplyHardLockdownAsync();
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnPause2099_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _updateService.ApplyPauseTo2099Async();
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnSecurityOnly_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _updateService.ApplySecurityOnlyModeAsync();
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnManualMode_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _updateService.ApplyManualNotificationModeAsync();
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnBlockDrivers_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _updateService.BlockDriverUpdatesAsync(true);
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnAllowDrivers_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _updateService.BlockDriverUpdatesAsync(false);
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnRestoreDefault_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Kembalikan seluruh pengaturan Windows Update ke setelan standar pabrik?",
            "Konfirmasi Reset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _updateService.RestoreDefaultUpdateAsync();
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
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
