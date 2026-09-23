using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using AturOS.Models;
using AturOS.Services;

namespace AturOS.Views;

public partial class NetworkDnsView : UserControl
{
    private readonly DnsOptimizerService _dnsService = new();
    public ObservableCollection<DnsPresetItem> Presets { get; } = new();
    private bool _hasBenchmarkedOnce = false;
    private bool _isBenchmarking = false;

    public NetworkDnsView()
    {
        InitializeComponent();

        foreach (var p in _dnsService.GetPresets())
        {
            Presets.Add(p);
        }
        ListDnsPresets.ItemsSource = Presets;

        Loaded += NetworkDnsView_Loaded;
    }

    private async void NetworkDnsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasBenchmarkedOnce || _isBenchmarking) return;

        try
        {
            await RunBenchmarkAllAsync();
            _hasBenchmarkedOnce = true;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Inisialisasi pengujian DNS gagal: {ex.Message}");
        }
    }

    private async Task RunBenchmarkAllAsync()
    {
        if (_isBenchmarking) return;
        _isBenchmarking = true;
        BtnBenchmarkAll.IsEnabled = false;

        try
        {
            foreach (var p in Presets)
            {
                if (!IsLoaded) break; // Hentikan jika pengguna berpindah ke menu lain
                await _dnsService.BenchmarkPresetAsync(p);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Benchmark DNS terinterupsi: {ex.Message}");
        }
        finally
        {
            _isBenchmarking = false;
            if (IsLoaded)
            {
                BtnBenchmarkAll.IsEnabled = true;
            }
        }
    }

    private async void BtnBenchmarkAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await RunBenchmarkAllAsync();
            ShowBanner("Pengujian latensi server DNS selesai.", isError: false);
        }
        catch (Exception ex)
        {
            ShowBanner($"Pengujian gagal: {ex.Message}", isError: true);
        }
    }

    private async void BtnTestSingle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DnsPresetItem preset) return;
        btn.IsEnabled = false;
        try
        {
            await _dnsService.BenchmarkPresetAsync(preset);
        }
        catch (Exception ex)
        {
            ShowBanner($"Uji ping gagal: {ex.Message}", isError: true);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private async void BtnApplyPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DnsPresetItem preset) return;

        btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _dnsService.ApplyDnsAsync(preset.PrimaryDns, preset.SecondaryDns);
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal menerapkan DNS: {ex.Message}", isError: true);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private async void BtnResetDhcp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn) btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _dnsService.ResetToDhcpAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal mereset DNS: {ex.Message}", isError: true);
        }
        finally
        {
            if (sender is Button b) b.IsEnabled = true;
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
