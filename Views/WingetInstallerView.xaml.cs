using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using AturOS.Models;
using AturOS.Services;

namespace AturOS.Views;

public partial class WingetInstallerView : UserControl
{
    private readonly WingetInstallerService _wingetService = new();
    public ObservableCollection<WingetAppItem> Apps { get; } = new();

    public WingetInstallerView()
    {
        InitializeComponent();

        foreach (var app in _wingetService.GetCatalog())
        {
            Apps.Add(app);
        }
        ListWingetApps.ItemsSource = Apps;
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var a in Apps) a.IsSelected = true;
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var a in Apps) a.IsSelected = false;
    }

    private async void BtnInstallSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = Apps.Where(a => a.IsSelected).ToList();
        if (!selected.Any())
        {
            MessageBox.Show("Pilih setidaknya satu aplikasi untuk dipasang.", "Pemasang Aplikasi Massal", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Mulai mengunduh dan memasang {selected.Count} aplikasi terpilih di latar belakang?",
            "Konfirmasi Pemasangan Massal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnInstallSelected.IsEnabled = false;
        ProgressCard.Visibility = Visibility.Visible;

        int successCount = 0;
        try
        {
            foreach (var app in selected)
            {
                TxtProgress.Text = $"Memasang {app.Name} via winget (mohon tunggu)...";
                bool ok = await _wingetService.InstallAppAsync(app);
                if (ok) successCount++;
            }

            ShowBanner($"Pemasangan selesai! {successCount} dari {selected.Count} aplikasi berhasil dipasang.", isError: successCount == 0);
        }
        catch (Exception ex)
        {
            ShowBanner($"Terjadi kesalahan saat memasang: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressCard.Visibility = Visibility.Collapsed;
            BtnInstallSelected.IsEnabled = true;
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
