using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using AturOS.Helpers;
using AturOS.Models;
using AturOS.Services;

namespace AturOS.Views;

public partial class DebloatView : UserControl
{
    private readonly RegistryTweakService _registryService = new();
    private readonly ServiceManagerService _serviceManager = new();
    private readonly PowerPlanService _powerPlanService = new();

    public ObservableCollection<TweakItem> Tweaks { get; } = new();

    public DebloatView()
    {
        InitializeComponent();

        // 1. Registry tweaks
        foreach (var tweak in _registryService.GetAvailableTweaks())
        {
            Tweaks.Add(tweak);
        }

        // 2. Service DiagTrack tweak
        Tweaks.Add(_serviceManager.GetTelemetryTweakItem());

        // 3. Ultimate Performance tweak
        Tweaks.Add(_powerPlanService.GetPowerPlanTweakItem());

        ListTweaks.ItemsSource = Tweaks;

        Loaded += DebloatView_Loaded;
    }

    private async void DebloatView_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAllTweaksStatusAsync();
    }

    private async Task RefreshAllTweaksStatusAsync()
    {
        foreach (var tweak in Tweaks)
        {
            if (tweak.Id == "service_diagtrack")
            {
                _serviceManager.RefreshTelemetryStatus(tweak);
            }
            else if (tweak.Id == "ultimate_performance")
            {
                await _powerPlanService.RefreshPowerPlanStatusAsync(tweak);
            }
            else
            {
                _registryService.RefreshTweakStatus(tweak);
            }
        }
    }

    private async void BtnRefreshTweaks_Click(object sender, RoutedEventArgs e)
    {
        BtnRefreshTweaks.IsEnabled = false;
        await RefreshAllTweaksStatusAsync();
        BtnRefreshTweaks.IsEnabled = true;
        ShowBanner("Status konfigurasi berhasil diperbarui dari Windows.", isError: false);
    }

    private async void BtnApplyTweak_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not TweakItem tweak) return;

        if (tweak.RequiresElevation && !AdministratorHelper.IsAdministrator)
        {
            var res = MessageBox.Show(
                $"Pengaturan '{tweak.Name}' memerlukan hak akses Administrator.\n\nApakah Anda ingin menjalankan ulang AturOS sebagai Administrator?",
                "Hak Akses Administrator Diperlukan",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res == MessageBoxResult.Yes)
            {
                AdministratorHelper.RestartAsAdministrator();
            }
            return;
        }

        tweak.IsProcessing = true;
        try
        {
            bool success = false;
            string message = string.Empty;

            if (tweak.Id == "service_diagtrack")
            {
                var confirm = MessageBox.Show(
                    "Nonaktifkan layanan Connected User Experiences and Telemetry (DiagTrack)?\n\nLayanan ini akan dihentikan dan tipe startup diubah ke Disabled.",
                    "Konfirmasi Nonaktifkan Telemetri",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;

                (success, message) = await _serviceManager.DisableTelemetryAsync(tweak);
            }
            else if (tweak.Id == "ultimate_performance")
            {
                (success, message) = await _powerPlanService.EnableUltimatePerformanceAsync(tweak);
            }
            else
            {
                (success, message) = await _registryService.ApplyTweakAsync(tweak);
            }

            ShowBanner(message, isError: !success);
            await RefreshAllTweaksStatusAsync();
        }
        finally
        {
            tweak.IsProcessing = false;
        }
    }

    private async void BtnRestoreTweak_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not TweakItem tweak) return;

        if (tweak.RequiresElevation && !AdministratorHelper.IsAdministrator)
        {
            MessageBox.Show("Pengaturan ini membutuhkan hak Administrator untuk dikembalikan.", "Hak Akses Diperlukan", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        tweak.IsProcessing = true;
        try
        {
            bool success = false;
            string message = string.Empty;

            if (tweak.Id == "service_diagtrack")
            {
                (success, message) = await _serviceManager.RestoreTelemetryAsync(tweak);
            }
            else if (tweak.Id == "ultimate_performance")
            {
                (success, message) = await _powerPlanService.RestoreBalancedPlanAsync(tweak);
            }
            else
            {
                (success, message) = await _registryService.RestoreTweakAsync(tweak);
            }

            ShowBanner(message, isError: !success);
            await RefreshAllTweaksStatusAsync();
        }
        finally
        {
            tweak.IsProcessing = false;
        }
    }

    private async void BtnRestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Restart Windows Explorer sekarang?\n\nTaskbar dan jendela folder akan tertutup dan terbuka kembali dalam hitungan detik untuk menerapkan perubahan tampilan.",
            "Konfirmasi Restart Explorer",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnRestartExplorer.IsEnabled = false;
        bool ok = await RegistryTweakService.RestartExplorerAsync();
        BtnRestartExplorer.IsEnabled = true;

        ShowBanner(ok ? "Windows Explorer berhasil direstart." : "Gagal merestart Explorer.", isError: !ok);
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
