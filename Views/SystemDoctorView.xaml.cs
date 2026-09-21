using System;
using System.Collections.Generic;
using System.Linq;
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

    public SystemDoctorView()
    {
        InitializeComponent();
        Loaded += SystemDoctorView_Loaded;
    }

    private async void SystemDoctorView_Loaded(object sender, RoutedEventArgs e)
    {
        AppendLog("Dokter Sistem siap. Memuat status integritas drive dan dependensi...");
        await RefreshAllStatusAsync();
    }

    private async Task RefreshAllStatusAsync()
    {
        await RefreshDiskStatusAsync();
        await RefreshRuntimesAsync();
    }

    private async Task RefreshDiskStatusAsync()
    {
        try
        {
            var disk = await _service.CheckDiskHealthAsync();
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
            TxtDiskStatus.Text = "Gagal memuat status disk";
            AppendLog($"Error disk: {ex.Message}");
        }
    }

    private async Task RefreshRuntimesAsync()
    {
        try
        {
            var runtimes = await _service.CheckRuntimeDependenciesAsync();
            ListRuntimes.ItemsSource = runtimes;
        }
        catch (Exception ex)
        {
            AppendLog($"Gagal memindai dependensi runtime: {ex.Message}");
        }
    }

    private void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            TxtConsoleLog.AppendText(line + Environment.NewLine);
            TxtConsoleLog.ScrollToEnd();
            LoggerService.Instance.Info($"[DokterSistem] {message}");
        });
    }

    private void ShowBanner(string message, bool isError = false)
    {
        Dispatcher.Invoke(() =>
        {
            TxtBannerMessage.Text = message;
            BannerStatus.Background = isError
                ? (Brush)FindResource("BrushDangerLight")
                : (Brush)FindResource("BrushInfoLight");
            BannerStatus.Visibility = Visibility.Visible;
        });
    }

    #region Event Handlers

    private async void BtnRefreshStatus_Click(object sender, RoutedEventArgs e)
    {
        AppendLog("Menyegarkan status sistem...");
        await RefreshAllStatusAsync();
        AppendLog("Status sistem diperbarui.");
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
        BtnScanShortcutsRegistry.IsEnabled = false;
        ProgressOperation.Visibility = Visibility.Visible;
        AppendLog("Memulai pemindaian file shortcut rusak dan entri uninstall orphaned...");

        try
        {
            _brokenShortcuts = await _service.ScanBrokenShortcutsAsync(AppendLog);
            _orphanedRegistry = await _service.ScanOrphanedRegistryKeysAsync(AppendLog);

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
            AppendLog($"Pemindaian selesai: {totalIssues} masalah ditemukan.");
        }
        finally
        {
            BtnScanShortcutsRegistry.IsEnabled = true;
            ProgressOperation.Visibility = Visibility.Collapsed;
        }
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
