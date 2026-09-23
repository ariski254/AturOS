using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AturOS.Models;
using AturOS.Services;

namespace AturOS.Views;

public partial class SystemDoctorView : UserControl
{
    private readonly SystemDoctorService _service = SystemDoctorService.Instance;
    private List<BrokenShortcutItem> _brokenShortcuts = new();
    private List<OrphanedRegistryItem> _orphanedRegistry = new();
    private bool _hasAutoScannedOnce = false;
    private bool _isScanning = false;

    public SystemDoctorView()
    {
        InitializeComponent();
        Loaded += SystemDoctorView_Loaded;
    }

    private async void SystemDoctorView_Loaded(object sender, RoutedEventArgs e)
    {
        AppendLog("Dokter Sistem siap. Memuat status integritas sistem, drive, dan dependensi...");
        await RefreshDiskStatusAsync();
        await RefreshRuntimesAsync();

        // Hanya scan otomatis shortcut & registri sekali pada saat awal dibuka
        if (!_hasAutoScannedOnce && !_isScanning)
        {
            _hasAutoScannedOnce = true;
            await ScanShortcutsAndRegistryAsync(isAuto: true);
        }
    }

    private async Task RefreshAllStatusAsync()
    {
        await RefreshDiskStatusAsync();
        await RefreshRuntimesAsync();
        await ScanShortcutsAndRegistryAsync(isAuto: false);
    }

    private async Task RefreshDiskStatusAsync()
    {
        try
        {
            var disk = await _service.CheckDiskHealthAsync();
            if (!IsLoaded) return;
            TxtDiskStatus.Text = $"{disk.DriveLetter} ({disk.FileSystem}) • {disk.FreeSpaceGb} GB Bebas dari {disk.TotalSpaceGb} GB ({disk.UsedPercent}% terpakai)";

            if (disk.IsDirty)
            {
                TxtBadgeDisk.Text = "Perlu Perbaikan";
                TxtBadgeDisk.Foreground = (Brush)FindResource("BrushDanger");
                BadgeDiskDirty.Background = (Brush)FindResource("BrushDangerLight");
            }
            else
            {
                TxtBadgeDisk.Text = "Bersih / Normal";
                TxtBadgeDisk.Foreground = (Brush)FindResource("BrushSuccess");
                BadgeDiskDirty.Background = (Brush)FindResource("BrushSuccessLight");
            }
        }
        catch (Exception ex)
        {
            if (IsLoaded)
            {
                TxtDiskStatus.Text = "Gagal memuat status disk";
            }
            AppendLog($"Error disk: {ex.Message}");
        }
    }

    private async Task RefreshRuntimesAsync()
    {
        try
        {
            var runtimes = await _service.CheckRuntimeDependenciesAsync();
            if (!IsLoaded) return;
            ListRuntimes.ItemsSource = runtimes;
        }
        catch (Exception ex)
        {
            AppendLog($"Gagal memindai dependensi runtime: {ex.Message}");
        }
    }

    public async Task ScanShortcutsAndRegistryAsync(bool isAuto = false)
    {
        if (_isScanning) return;
        _isScanning = true;

        BtnScanShortcutsRegistry.IsEnabled = false;
        ProgressOperation.Visibility = Visibility.Visible;
        AppendLog(isAuto
            ? "Memindai file jalan pintas rusak dan entri registri orphaned secara otomatis..."
            : "Memulai pemindaian file shortcut rusak dan entri uninstall orphaned...");

        try
        {
            _brokenShortcuts = await _service.ScanBrokenShortcutsAsync(AppendLog);
            _orphanedRegistry = await _service.ScanOrphanedRegistryKeysAsync(AppendLog);

            if (!IsLoaded) return;

            int totalIssues = _brokenShortcuts.Count + _orphanedRegistry.Count;
            TxtShortcutsSummary.Text = $"Ditemukan {totalIssues} item masalah ({_brokenShortcuts.Count} jalan pintas rusak, {_orphanedRegistry.Count} registri orphaned).";

            if (_brokenShortcuts.Count > 0)
            {
                ListShortcuts.ItemsSource = _brokenShortcuts;
                ListShortcuts.Visibility = Visibility.Visible;
            }
            else
            {
                ListShortcuts.Visibility = Visibility.Collapsed;
            }

            BtnCleanShortcutsRegistry.IsEnabled = totalIssues > 0;
            AppendLog($"Pemindaian selesai: {totalIssues} masalah integritas shortcut & registri ditemukan.");
            if (!isAuto || totalIssues > 0)
            {
                ShowBanner($"Pemindaian integritas selesai: {totalIssues} masalah ditemukan.", isError: false);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Gagal memindai shortcut/registri: {ex.Message}");
        }
        finally
        {
            _isScanning = false;
            BtnScanShortcutsRegistry.IsEnabled = true;
            ProgressOperation.Visibility = Visibility.Collapsed;
        }
    }

    private void AppendLog(string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            TxtConsoleLog.AppendText(line + Environment.NewLine);
            TxtConsoleLog.ScrollToEnd();
            LoggerService.Instance.Info($"[DokterSistem] {message}");
        });
    }

    private CancellationTokenSource? _bannerCts;

    private void ShowBanner(string message, bool isError = false)
    {
        Dispatcher.BeginInvoke(async () =>
        {
            _bannerCts?.Cancel();
            var cts = new CancellationTokenSource();
            _bannerCts = cts;

            TxtBannerMessage.Text = message;
            BannerStatus.Background = isError
                ? (Brush)FindResource("BrushDangerLight")
                : (Brush)FindResource("BrushInfoLight");
            BannerStatus.Visibility = Visibility.Visible;

            try
            {
                await Task.Delay(isError ? 6000 : 4000, cts.Token);
                BannerStatus.Visibility = Visibility.Collapsed;
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    private void BtnCloseBanner_Click(object sender, RoutedEventArgs e)
    {
        _bannerCts?.Cancel();
        BannerStatus.Visibility = Visibility.Collapsed;
    }

    #region Event Handlers

    private async void BtnRefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            AppendLog("Menyegarkan status sistem...");
            await RefreshAllStatusAsync();
            AppendLog("Status sistem diperbarui.");
        }
        catch (Exception ex)
        {
            AppendLog($"Gagal menyegarkan status: {ex.Message}");
            ShowBanner($"Gagal menyegarkan status: {ex.Message}", isError: true);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnRunSfcDism_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Jalankan pemindaian integritas berkas sistem (sfc /scannow)?\n\nJika ditemukan berkas rusak yang belum terselesaikan, pemulihan citra Windows (DISM /RestoreHealth) akan dijalankan secara otomatis.\n\nProses ini memerlukan waktu beberapa menit.",
            "Konfirmasi Pemindaian Sistem",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnRunSfcDism.IsEnabled = false;
        ProgressOperation.Visibility = Visibility.Visible;
        TxtSfcStatus.Text = "Sedang memindai integritas berkas (sfc /scannow)...";

        try
        {
            var (success, message, dismRun) = await _service.ScanAndRepairSystemFilesAsync(AppendLog);
            TxtSfcStatus.Text = success ? "Pemeriksaan selesai: Kondisi Sehat" : "Pemeriksaan selesai dengan catatan";
            ShowBanner(message, !success);
            AppendLog(message);
        }
        finally
        {
            BtnRunSfcDism.IsEnabled = true;
            ProgressOperation.Visibility = Visibility.Collapsed;
        }
    }

    private async void BtnResetUpdateQueue_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Reset antrian download Windows Update dan folder cache SoftwareDistribution & Catroot2?\n\nLayanan update akan direstart secara bersih.",
            "Reset Antrian Windows Update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnResetUpdateQueue.IsEnabled = false;
        ProgressOperation.Visibility = Visibility.Visible;

        try
        {
            var (success, message) = await _service.RepairWindowsUpdateQueueAsync(AppendLog);
            ShowBanner(message, !success);
            AppendLog(message);
        }
        finally
        {
            BtnResetUpdateQueue.IsEnabled = true;
            ProgressOperation.Visibility = Visibility.Collapsed;
        }
    }

    private async void BtnRepairNetwork_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Reset tumpukan TCP/IP, katalog Winsock, dan cache DNS?\n\nOperasi ini akan memperbarui sambungan jaringan lokal.",
            "Reset Tumpukan Jaringan",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnRepairNetwork.IsEnabled = false;
        ProgressOperation.Visibility = Visibility.Visible;

        try
        {
            var (success, message) = await _service.RepairNetworkStackAsync(AppendLog);
            ShowBanner(message, !success);
            AppendLog(message);
        }
        finally
        {
            BtnRepairNetwork.IsEnabled = true;
            ProgressOperation.Visibility = Visibility.Collapsed;
        }
    }

    private async void BtnScheduleChkdsk_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Jadwalkan pemeriksaan dan perbaikan bad sector drive C: (chkdsk C: /f /r) saat komputer restart?\n\nWindows akan memeriksa dan memperbaiki integritas drive saat proses boot berikutnya.",
            "Jadwalkan CHKDSK",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnScheduleChkdsk.IsEnabled = false;
        try
        {
            var (success, message) = await _service.ScheduleChkdskOnRebootAsync("C:");
            ShowBanner(message, !success);
            AppendLog(message);
            await RefreshDiskStatusAsync();
        }
        finally
        {
            BtnScheduleChkdsk.IsEnabled = true;
        }
    }

    private async void BtnScanRuntimes_Click(object sender, RoutedEventArgs e)
    {
        BtnScanRuntimes.IsEnabled = false;
        AppendLog("Memindai dependensi Visual C++, DirectX, dan .NET Runtime...");
        try
        {
            await RefreshRuntimesAsync();
            AppendLog("Pemindaian runtime dependensi selesai.");
        }
        finally
        {
            BtnScanRuntimes.IsEnabled = true;
        }
    }

    private async void BtnInstallRuntimeItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string wingetId && !string.IsNullOrEmpty(wingetId))
        {
            btn.IsEnabled = false;
            ProgressOperation.Visibility = Visibility.Visible;
            AppendLog($"Memulai instalasi {wingetId} via Winget CLI...");

            try
            {
                var (success, message) = await _service.InstallRuntimeAsync(wingetId, AppendLog);
                ShowBanner(message, !success);
                AppendLog(message);
                await RefreshRuntimesAsync();
            }
            finally
            {
                btn.IsEnabled = true;
                ProgressOperation.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async void BtnScanShortcutsRegistry_Click(object sender, RoutedEventArgs e)
    {
        await ScanShortcutsAndRegistryAsync(isAuto: false);
    }

    private async void BtnCleanShortcutsRegistry_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Bersihkan seluruh jalan pintas rusak dan entri registry orphaned yang dipilih?",
            "Konfirmasi Pembersihan",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnCleanShortcutsRegistry.IsEnabled = false;
        ProgressOperation.Visibility = Visibility.Visible;

        try
        {
            int shortcutsCleaned = await _service.CleanBrokenShortcutsAsync(_brokenShortcuts.Where(s => s.IsSelected));
            int registryCleaned = await _service.CleanOrphanedRegistryKeysAsync(_orphanedRegistry.Where(r => r.IsSelected));

            string msg = $"Berhasil membersihkan {shortcutsCleaned} jalan pintas rusak dan {registryCleaned} entri registri orphaned.";
            ShowBanner(msg);
            AppendLog(msg);

            // Re-scan
            _brokenShortcuts.Clear();
            _orphanedRegistry.Clear();
            ListShortcuts.ItemsSource = null;
            ListShortcuts.Visibility = Visibility.Collapsed;
            TxtShortcutsSummary.Text = msg;
        }
        finally
        {
            ProgressOperation.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        TxtConsoleLog.Clear();
    }

    #endregion
}
