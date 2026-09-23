using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AturOS.Models;
using AturOS.Services;

namespace AturOS.Views;

public partial class StorageCleanerView : UserControl
{
    private readonly StorageCleanerService _cleanerService = new();
    private readonly CompactOsService _compactService = new();
    private readonly HibernationService _hibernationService = new();

    public ObservableCollection<CleanableItem> Targets { get; } = new();
    private bool _hasScannedOnce = false;
    private bool _isScanning = false;
    private CancellationTokenSource? _scanCts;

    public StorageCleanerView()
    {
        InitializeComponent();

        foreach (var item in _cleanerService.GetDefaultTargets())
        {
            Targets.Add(item);
        }
        ListCleanerTargets.ItemsSource = Targets;

        Loaded += StorageCleanerView_Loaded;
        Unloaded += StorageCleanerView_Unloaded;
    }

    private async void StorageCleanerView_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAdvancedStorageStatusAsync();
        // Hanya scan otomatis pertama kali saat halaman dibuka jika belum pernah discan
        if (!_hasScannedOnce && !_isScanning)
        {
            await StartScanAsync(isAuto: true);
        }
    }

    private void StorageCleanerView_Unloaded(object sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
    }

    private async Task RefreshAdvancedStorageStatusAsync()
    {
        // 1. CompactOS
        var compactStatus = await _compactService.QueryStatusAsync();
        TxtCompactStatus.Text = compactStatus switch
        {
            CompactOsStatus.Aktif => "Aktif",
            CompactOsStatus.Nonaktif => "Nonaktif",
            _ => "Tidak Diketahui"
        };

        // 2. Hibernation
        var hiberStatus = _hibernationService.QueryStatus();
        TxtHibernateStatus.Text = hiberStatus switch
        {
            HibernationStatus.Aktif => "Aktif (ON)",
            HibernationStatus.Nonaktif => "Nonaktif (OFF)",
            _ => "Tidak Diketahui"
        };

        // 3. Storage Sense
        bool senseOn = _cleanerService.GetStorageSenseStatus();
        TxtStorageSenseStatus.Text = senseOn ? "Aktif" : "Nonaktif";

        // 4. NTFS Last Access
        bool ntfsDisabled = _cleanerService.GetNtfsLastAccessUpdateStatus();
        TxtNtfsAccessStatus.Text = ntfsDisabled ? "Nonaktif (Hemat SSD)" : "Aktif (Default)";

        // 5. Reserved Storage
        var resStorage = await _cleanerService.GetReservedStorageStatusAsync();
        TxtReservedStorageStatus.Text = resStorage switch
        {
            true => "Aktif (~7 GB)",
            false => "Nonaktif (Bebas)",
            _ => "Tidak Diketahui"
        };
    }

    public async Task StartScanAsync(bool isAuto = false)
    {
        if (_isScanning) return;
        _isScanning = true;
        _scanCts = new CancellationTokenSource();
        var ct = _scanCts.Token;

        BtnScan.IsEnabled = false;
        BtnClean.IsEnabled = false;
        ProgressCard.Visibility = Visibility.Visible;
        TxtProgressDesc.Text = isAuto
            ? "Memindai direktori file sementara secara otomatis..."
            : "Memindai direktori file sementara...";

        try
        {
            long totalBytes = 0;
            int totalFiles = 0;

            foreach (var item in Targets)
            {
                if (ct.IsCancellationRequested || !IsLoaded) break;
                TxtProgressDesc.Text = $"Memindai {item.DisplayName}...";
                await _cleanerService.ScanTargetAsync(item);
                totalBytes += item.SizeBytes;
                totalFiles += item.FileCount;
            }

            if (ct.IsCancellationRequested || !IsLoaded) return;

            _hasScannedOnce = true;
            BtnClean.IsEnabled = totalFiles > 0;

            string totalFormatted = totalBytes >= 1024L * 1024 * 1024
                ? $"{(double)totalBytes / (1024 * 1024 * 1024):F2} GB"
                : $"{(double)totalBytes / (1024 * 1024):F2} MB";

            if (!isAuto || totalFiles > 0)
            {
                ShowBanner($"Pemindaian selesai: Menemukan {totalFiles} file sementara ({totalFormatted}) yang siap dibersihkan.", isError: false);
            }
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested && IsLoaded)
            {
                ShowBanner($"Pemindaian gagal: {ex.Message}", isError: true);
            }
        }
        finally
        {
            _isScanning = false;
            if (IsLoaded)
            {
                ProgressCard.Visibility = Visibility.Collapsed;
                BtnScan.IsEnabled = true;
            }
        }
    }

    private async void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        await StartScanAsync(isAuto: false);
    }

    private async void BtnClean_Click(object sender, RoutedEventArgs e)
    {
        var selectedTargets = Targets.Where(t => t.IsSelected && t.FileCount > 0).ToList();
        if (!selectedTargets.Any())
        {
            MessageBox.Show("Pilih setidaknya satu target yang memiliki file untuk dibersihkan.", "AturOS", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        long totalSize = selectedTargets.Sum(t => t.SizeBytes);
        string sizeText = totalSize >= 1024L * 1024 * 1024
            ? $"{(double)totalSize / (1024 * 1024 * 1024):F2} GB"
            : $"{(double)totalSize / (1024 * 1024):F2} MB";

        var confirm = MessageBox.Show(
            $"Hapus {selectedTargets.Sum(t => t.FileCount)} file sementara ({sizeText}) dari target yang dipilih?\n\nFile yang sedang digunakan oleh program yang berjalan akan dilewati secara otomatis.",
            "Konfirmasi Pembersihan Drive",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        BtnScan.IsEnabled = false;
        BtnClean.IsEnabled = false;
        ProgressCard.Visibility = Visibility.Visible;

        try
        {
            var result = await _cleanerService.CleanTargetsAsync(selectedTargets, progress =>
            {
                Dispatcher.BeginInvoke(() => TxtProgressDesc.Text = progress);
            });

            ShowBanner(
                $"Pembersihan selesai! {result.FilesDeleted} file berhasil dihapus ({result.FormattedFreed} dibebaskan). {result.FilesSkipped} file dilewati karena sedang digunakan sistem.",
                isError: false);

            BtnClean.IsEnabled = Targets.Any(t => t.FileCount > 0);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal membersihkan file: {ex.Message}", isError: true);
        }
        finally
        {
            ProgressCard.Visibility = Visibility.Collapsed;
            BtnScan.IsEnabled = true;
        }
    }

    private async void BtnStorageSenseOn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _cleanerService.SetStorageSenseAsync(true);
            await RefreshAdvancedStorageStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal mengubah Storage Sense: {ex.Message}", isError: true);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private async void BtnStorageSenseOff_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _cleanerService.SetStorageSenseAsync(false);
            await RefreshAdvancedStorageStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal mengubah Storage Sense: {ex.Message}", isError: true);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private async void BtnNtfsAccessDisable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _cleanerService.SetNtfsLastAccessUpdateAsync(true);
            await RefreshAdvancedStorageStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal mengubah konfigurasi NTFS: {ex.Message}", isError: true);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private async void BtnNtfsAccessEnable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _cleanerService.SetNtfsLastAccessUpdateAsync(false);
            await RefreshAdvancedStorageStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal mengubah konfigurasi NTFS: {ex.Message}", isError: true);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private async void BtnReservedStorageOff_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Nonaktifkan Penyimpanan Cadangan Windows (Reserved Storage)?\n\nTindakan ini akan membebaskan hingga ~7 GB ruang disk C: yang dicadangkan oleh sistem. Memerlukan hak Administrator.",
            "Konfirmasi Reserved Storage",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        ProgressCard.Visibility = Visibility.Visible;
        TxtProgressDesc.Text = "Menonaktifkan Reserved Storage via DISM...";

        try
        {
            var (success, msg) = await _cleanerService.SetReservedStorageAsync(false);
            await RefreshAdvancedStorageStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            ProgressCard.Visibility = Visibility.Collapsed;
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnReservedStorageOn_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        ProgressCard.Visibility = Visibility.Visible;
        TxtProgressDesc.Text = "Mengaktifkan Reserved Storage via DISM...";

        try
        {
            var (success, msg) = await _cleanerService.SetReservedStorageAsync(true);
            await RefreshAdvancedStorageStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            ProgressCard.Visibility = Visibility.Collapsed;
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnCompactEnable_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Aktifkan kompresi sistem CompactOS?\n\nWindows akan mengompresi biner sistem operasi di latar belakang. Proses ini memerlukan waktu beberapa menit dan hak Administrator.",
            "Konfirmasi CompactOS",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        ProgressCard.Visibility = Visibility.Visible;
        TxtProgressDesc.Text = "Menjalankan compact.exe /compactos:always (mohon tunggu)...";

        try
        {
            var (success, msg) = await _compactService.SetCompactOsAsync(true);
            await RefreshAdvancedStorageStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            ProgressCard.Visibility = Visibility.Collapsed;
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnCompactDisable_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Nonaktifkan kompresi sistem CompactOS?\n\nFile sistem akan dikembalikan ke kondisi tidak terkompresi.",
            "Konfirmasi CompactOS",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        ProgressCard.Visibility = Visibility.Visible;
        TxtProgressDesc.Text = "Menjalankan compact.exe /compactos:never...";

        try
        {
            var (success, msg) = await _compactService.SetCompactOsAsync(false);
            await RefreshAdvancedStorageStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            ProgressCard.Visibility = Visibility.Collapsed;
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnHibernateOn_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        ProgressCard.Visibility = Visibility.Visible;
        TxtProgressDesc.Text = "Mengaktifkan hibernasi...";

        try
        {
            var (success, msg) = await _hibernationService.SetHibernationAsync(true);
            await RefreshAdvancedStorageStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            ProgressCard.Visibility = Visibility.Collapsed;
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnHibernateOff_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Matikan Hibernasi?\n\nWindows akan menghapus file hiberfil.sys dan membebaskan ruang penyimpanan drive C: sebesar kapasitas RAM Anda.\nFitur Hibernasi dan Fast Startup tidak dapat digunakan sampai diaktifkan kembali.",
            "Konfirmasi Matikan Hibernasi",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        ProgressCard.Visibility = Visibility.Visible;
        TxtProgressDesc.Text = "Menonaktifkan hibernasi...";

        try
        {
            var (success, msg) = await _hibernationService.SetHibernationAsync(false);
            await RefreshAdvancedStorageStatusAsync();
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            ProgressCard.Visibility = Visibility.Collapsed;
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnDismCleanup_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Jalankan pembersihan komponen WinSxS via DISM?\n\nOperasi ini akan memindai dan menghapus cadangan pembaruan Windows lama yang sudah digantikan. Proses ini membutuhkan hak akses Administrator dan memakan waktu beberapa menit.",
            "Konfirmasi Pembersihan WinSxS DISM",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        ProgressCard.Visibility = Visibility.Visible;
        TxtProgressDesc.Text = "Menjalankan DISM Component Cleanup (mohon tunggu)...";

        try
        {
            var (success, msg) = await _cleanerService.RunDismComponentCleanupAsync(p =>
            {
                Dispatcher.BeginInvoke(() => TxtProgressDesc.Text = p);
            });
            ShowBanner(msg, isError: !success);
        }
        finally
        {
            ProgressCard.Visibility = Visibility.Collapsed;
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
