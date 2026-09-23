using System.Windows;
using System.Windows.Controls;
using AturOS.Helpers;
using AturOS.Services;

namespace AturOS.Views;

public partial class BackupRestoreView : UserControl
{
    private readonly RestorePointService _restoreService = new();
    private readonly RegistryBackupService _registryBackupService = new();
    private bool _hasLoadedPointsOnce = false;
    private bool _isLoadingPoints = false;

    public BackupRestoreView()
    {
        InitializeComponent();
        AdminNoticeCard.Visibility = AdministratorHelper.IsAdministrator ? Visibility.Collapsed : Visibility.Visible;
        RefreshRegistryBackupsList();
        Loaded += BackupRestoreView_Loaded;
    }

    private async void BackupRestoreView_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshRegistryBackupsList();
        if (_hasLoadedPointsOnce || _isLoadingPoints) return;
        await RefreshPointsListAsync();
    }

    private void RefreshRegistryBackupsList()
    {
        try
        {
            var items = _registryBackupService.GetAvailableBackupItems();
            GridRegistryBackups.ItemsSource = items;
            TxtEmptyRegistryBackups.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal membaca daftar cadangan registri: {ex.Message}");
        }
    }

    private async Task RefreshPointsListAsync()
    {
        if (_isLoadingPoints) return;
        _isLoadingPoints = true;

        bool isAdmin = AdministratorHelper.IsAdministrator;
        AdminNoticeCard.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;

        try
        {
            var points = await _restoreService.GetRestorePointsAsync();
            if (!IsLoaded) return;
            GridRestorePoints.ItemsSource = points;
            TxtEmptyRestorePoints.Visibility = (points == null || points.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
            _hasLoadedPointsOnce = true;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal memuat restore points: {ex.Message}");
            if (IsLoaded)
            {
                TxtEmptyRestorePoints.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            _isLoadingPoints = false;
        }
    }

    private async void BtnRefreshList_Click(object sender, RoutedEventArgs e)
    {
        BtnRefreshList.IsEnabled = false;
        await RefreshPointsListAsync();
        BtnRefreshList.IsEnabled = true;
    }

    private void BtnRefreshRegBackups_Click(object sender, RoutedEventArgs e)
    {
        RefreshRegistryBackupsList();
    }

    private void BtnRestartAdmin_Click(object sender, RoutedEventArgs e)
    {
        AdministratorHelper.RestartAsAdministrator();
    }

    private void BtnOpenRestoreWizard_Click(object sender, RoutedEventArgs e)
    {
        RestorePointService.OpenRestoreWizard();
    }

    private async void BtnRestoreSystemPoint_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not Models.RestorePointItem point) return;

        var confirm = MessageBox.Show(
            $"PERINGATAN: Anda akan memulihkan sistem Windows ke Titik Pemulihan #{point.SequenceNumber} ('{point.Description}') yang dibuat pada {point.CreationTime}.\n\n" +
            "Proses ini akan mengembalikan file sistem, registry, dan driver ke kondisi tanggal tersebut. Komputer akan dimulai ulang secara otomatis.\n\n" +
            "Apakah Anda yakin ingin melanjutkan pemulihan sekarang?",
            "Konfirmasi Pemulihan Sistem Windows",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        btn.IsEnabled = false;
        try
        {
            ShowBanner($"Memulai pemulihan sistem ke Titik #{point.SequenceNumber}... Mohon jangan matikan komputer.", isError: false);
            var (success, msg) = await _restoreService.RestoreToSequenceAsync(point.SequenceNumber);
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal memulihkan sistem: {ex.Message}", isError: true);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private async void BtnBackupRegistry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn) btn.IsEnabled = false;
        try
        {
            var key = (ComboRegistryKey.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "HKEY_CURRENT_USER\\Software\\Policies";
            var (success, path) = await _registryBackupService.BackupKeyAsync(key, "AturOS_Backup");
            ShowBanner(success ? $"Cadangan registri berhasil diekspor ke: {path}" : $"Gagal mengekspor registri: {path}", isError: !success);
            if (success)
            {
                RefreshRegistryBackupsList();
            }
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal mencadangkan registri: {ex.Message}", isError: true);
        }
        finally
        {
            if (sender is Button b) b.IsEnabled = true;
        }
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        RegistryBackupService.OpenBackupFolder();
    }

    private async void BtnRestoreReg_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not RegistryBackupItem item) return;

        var confirm = MessageBox.Show(
            $"Pulihkan berkas registri '{item.FileName}' ke sistem Windows sekarang?\n\nPengaturan registri dari file cadangan ini akan diimpor kembali ke sistem.",
            "Konfirmasi Pemulihan Registri",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        btn.IsEnabled = false;
        try
        {
            var (success, msg) = await _registryBackupService.RestoreBackupAsync(item.FullPath);
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal memulihkan registri: {ex.Message}", isError: true);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private void BtnOpenNotepad_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not RegistryBackupItem item) return;
        RegistryBackupService.OpenInNotepad(item.FullPath);
    }

    private void BtnDeleteReg_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not RegistryBackupItem item) return;

        var confirm = MessageBox.Show(
            $"Hapus berkas cadangan '{item.FileName}' secara permanen?",
            "Konfirmasi Hapus Berkas Cadangan",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        bool ok = _registryBackupService.DeleteBackup(item.FullPath);
        if (ok)
        {
            ShowBanner($"Berkas cadangan '{item.FileName}' berhasil dihapus.", isError: false);
            RefreshRegistryBackupsList();
        }
        else
        {
            ShowBanner($"Gagal menghapus berkas cadangan '{item.FileName}'.", isError: true);
        }
    }

    private async void BtnCreatePoint_Click(object sender, RoutedEventArgs e)
    {
        if (!AdministratorHelper.IsAdministrator)
        {
            var res = MessageBox.Show(
                "Membuat System Restore Point memerlukan hak akses Administrator.\n\nApakah Anda ingin menjalankan ulang AturOS sebagai Administrator?",
                "Hak Akses Administrator Diperlukan",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res == MessageBoxResult.Yes)
            {
                AdministratorHelper.RestartAsAdministrator();
            }
            return;
        }

        var desc = TxtDescription.Text.Trim();
        if (string.IsNullOrWhiteSpace(desc))
        {
            desc = "AturOS-Backup";
        }

        var confirm = MessageBox.Show(
            $"Buat System Restore Point dengan deskripsi '{desc}' sekarang?",
            "Konfirmasi System Restore Point",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnCreatePoint.IsEnabled = false;
        CreationProgress.Visibility = Visibility.Visible;

        try
        {
            var (success, message) = await _restoreService.CreateRestorePointAsync(desc);
            ShowBanner(message, isError: !success);

            if (success)
            {
                await RefreshPointsListAsync();
            }
        }
        finally
        {
            CreationProgress.Visibility = Visibility.Collapsed;
            BtnCreatePoint.IsEnabled = true;
        }
    }

    private void BtnOpenSystemProtection_Click(object sender, RoutedEventArgs e)
    {
        RestorePointService.OpenSystemProtectionSettings();
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
