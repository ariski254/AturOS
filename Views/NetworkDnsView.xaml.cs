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
        await RunBenchmarkAllAsync();
    }

    private async Task RunBenchmarkAllAsync()
    {
        BtnBenchmarkAll.IsEnabled = false;
        try
        {
            foreach (var p in Presets)
            {
                await _dnsService.BenchmarkPresetAsync(p);
            }
        }
        finally
        {
            BtnBenchmarkAll.IsEnabled = true;
        }
    }

    private async void BtnBenchmarkAll_Click(object sender, RoutedEventArgs e)
    {
        await RunBenchmarkAllAsync();
        ShowBanner("Pengujian latensi server DNS selesai.", isError: false);
    }

    private async void BtnTestSingle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DnsPresetItem preset) return;
        btn.IsEnabled = false;
        await _dnsService.BenchmarkPresetAsync(preset);
        btn.IsEnabled = true;
    }

    private async void BtnApplyPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DnsPresetItem preset) return;

        var (success, msg) = await _dnsService.ApplyDnsAsync(preset.PrimaryDns, preset.SecondaryDns);
        ShowBanner(msg, isError: !success);
    }

    private async void BtnResetDhcp_Click(object sender, RoutedEventArgs e)
    {
        var (success, msg) = await _dnsService.ResetToDhcpAsync();
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
