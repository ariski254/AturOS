using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AturOS.Helpers;
using AturOS.Models;
using AturOS.Services;

namespace AturOS.Views;

public partial class WindowsLiteView : UserControl
{
    private readonly WindowsLiteService _liteService = new();
    private readonly RestorePointService _restoreService = new();
    private WindowsLiteProfileType? _activePreviewProfile;
    private CancellationTokenSource? _bannerCts;

    private bool _hasLoadedHwOnce = false;

    public WindowsLiteView()
    {
        InitializeComponent();
        Loaded += WindowsLiteView_Loaded;
    }

    private async void WindowsLiteView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoadedHwOnce) return;
        await RefreshHardwareInformationAsync();
    }

    #region Hardware Scan (Step 1: SCAN)

    private async void BtnRefreshHardware_Click(object sender, RoutedEventArgs e)
    {
        await RefreshHardwareInformationAsync();
    }

    private async Task RefreshHardwareInformationAsync()
    {
        try
        {
            var hw = await Task.Run(() => _liteService.GetHardwareEnvironment());
            if (!IsLoaded) return;
            _hasLoadedHwOnce = true;

            string deviceType = hw.IsLaptop ? "Laptop" : "Desktop PC";
            TxtHwDeviceType.Text = $"Tipe: {deviceType}";

            if (hw.HasBattery)
            {
                string powerText = hw.IsPluggedIn
                    ? $"Tersambung Listrik ({hw.BatteryPercent}%)"
                    : $"Baterai ({hw.BatteryPercent}%)";
                TxtHwPowerSource.Text = $"Daya: {powerText}";

                if (!hw.IsPluggedIn)
                {
                    HwBatteryWarningBox.Visibility = Visibility.Visible;
                    TxtHwBatteryWarning.Text = "Perhatian Laptop: Daya baterai sedang digunakan. Disarankan menghubungkan charger AC sebelum menerapkan profil Gaming atau Extreme Gaming agar prosesor dan GPU bekerja pada clockspeed maksimum.";
                }
                else
                {
                    HwBatteryWarningBox.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                TxtHwPowerSource.Text = "Daya: Listrik Desktop (AC)";
                HwBatteryWarningBox.Visibility = Visibility.Collapsed;
            }

            TxtHwBluetooth.Text = hw.HasBluetooth ? "Bluetooth: Terdeteksi" : "Bluetooth: Tidak Ada";
            
            // Populate Multi-Factor Hardware Matrix
            TxtHwCpu.Text = $"{hw.CpuModel} ({hw.CpuCores}C/{hw.CpuThreads}T)";
            TxtHwRam.Text = $"{hw.TotalRamGb:F1} GB";
            TxtHwGpu.Text = string.IsNullOrEmpty(hw.GpuModel) ? "Generic Display" : hw.GpuModel;
            TxtHwStorage.Text = $"{hw.SystemDriveType} ({hw.SystemDriveTotalGb:F0} GB)";

            TxtHwTier.Text = $"TIER: {hw.HardwareTierBadge}";
            TxtHwRecommendation.Text = $"Rekomendasi: {hw.RecommendationText}";
            TxtHwReasonsSummary.Text = $"Alasan: {hw.RecommendationReasonSummary}";

            // Colorize Tier Badge
            switch (hw.HardwareTier)
            {
                case HardwareTier.UltraLow:
                case HardwareTier.Low:
                    BorderHwTier.Background = (System.Windows.Media.Brush)FindResource("BrushWarningLight");
                    TxtHwTier.Foreground = (System.Windows.Media.Brush)FindResource("BrushWarning");
                    break;
                case HardwareTier.Mid:
                case HardwareTier.High:
                    BorderHwTier.Background = (System.Windows.Media.Brush)FindResource("BrushSuccessLight");
                    TxtHwTier.Foreground = (System.Windows.Media.Brush)FindResource("BrushSuccess");
                    break;
                default:
                    BorderHwTier.Background = (System.Windows.Media.Brush)FindResource("BrushAccentLight");
                    TxtHwTier.Foreground = (System.Windows.Media.Brush)FindResource("BrushAccent");
                    break;
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal memuat status hardware: {ex.Message}");
        }
    }

    private void BtnApplyRecommendedProfile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var hw = _liteService.GetHardwareEnvironment();
            ShowDryRunPreview(hw.RecommendedProfile);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal menganalisis profil rekomendasi: {ex.Message}");
            ShowDryRunPreview(WindowsLiteProfileType.Balanced);
        }
    }

    private async void BtnReconcile_Click(object sender, RoutedEventArgs e)
    {
        if (!AdministratorHelper.IsAdministrator)
        {
            MessageBox.Show(
                "Rekonsiliasi pasca-update Windows membutuhkan hak Administrator untuk memulihkan kebijakan optimasi dan memangkas bloatware yang terpasang ulang.",
                "Hak Akses Administrator Diperlukan",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            "Jalankan Rekonsiliasi Pasca-Update Windows?\n\nAturOS akan memeriksa layanan, registry, dan bloatware yang mungkin diaktifkan/diinstal ulang oleh Windows Update secara otomatis, lalu memulihkan status optimasi yang dipilih sebelumnya.",
            "Konfirmasi Rekonsiliasi Update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        SetLoading(true, "Menjalankan rekonsiliasi pasca-update Windows...");
        try
        {
            var (reconciled, msg) = await _liteService.ReconcilePostUpdateDebloatAsync(p =>
            {
                Dispatcher.BeginInvoke(() => TxtProgress.Text = p);
            });

            ShowBanner(msg, isError: reconciled < 0);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal melakukan rekonsiliasi: {ex.Message}", isError: true);
        }
        finally
        {
            SetLoading(false);
        }
    }

    #endregion

    #region Preview Handlers (Step 3: PREVIEW)

    private void BtnPreviewLight_Click(object sender, RoutedEventArgs e)
    {
        ShowDryRunPreview(WindowsLiteProfileType.Light);
    }

    private void BtnPreviewBalanced_Click(object sender, RoutedEventArgs e)
    {
        ShowDryRunPreview(WindowsLiteProfileType.Balanced);
    }

    private void BtnPreviewGaming_Click(object sender, RoutedEventArgs e)
    {
        ShowDryRunPreview(WindowsLiteProfileType.Gaming);
    }

    private void BtnPreviewExtreme_Click(object sender, RoutedEventArgs e)
    {
        ShowDryRunPreview(WindowsLiteProfileType.ExtremeGaming);
    }

    private void BtnPreviewLowEnd_Click(object sender, RoutedEventArgs e)
    {
        ShowDryRunPreview(WindowsLiteProfileType.LowEnd);
    }

    private void ShowDryRunPreview(WindowsLiteProfileType profile)
    {
        _activePreviewProfile = profile;
        var preview = _liteService.GetProfilePreview(profile);

        TxtPreviewTitle.Text = preview.Title;
        TxtPreviewSubtitle.Text = preview.Subtitle;
        TxtPreviewRam.Text = $"{preview.EstimatedRamSavings} RAM Bebas";

        TxtPreviewApps.Text = $"{preview.AppsToRemoveCount} Paket";
        TxtPreviewServices.Text = $"{preview.ServicesToModifyCount} Layanan";
        TxtPreviewTasks.Text = $"{preview.ScheduledTasksCount} Tasks";
        TxtPreviewTweaks.Text = $"{preview.RegistryTweaksCount} Tweaks";
        TxtPreviewUpdate.Text = preview.WindowsUpdateTarget;
        TxtPreviewPower.Text = preview.PowerPlanTarget;
        TxtPreviewGameMode.Text = preview.GameModeTarget;
        TxtPreviewVisual.Text = preview.VisualEffectsTarget;
        TxtPreviewHibernation.Text = preview.HibernationTarget;

        ItemsPreviewHighlights.ItemsSource = preview.Highlights;

        if (preview.IsExtreme)
        {
            PreviewWarningBox.Visibility = Visibility.Visible;
            TxtPreviewWarning.Text = "Mode ini akan mengunci Windows Update, mencopot Edge & OneDrive, mematikan Print Spooler, WSearch, dan SysMain, serta menghapus hibernasi. Mode ini dikhususkan untuk rig PC gaming murni dan tidak disarankan untuk PC kantor.";
            BtnConfirmApply.Style = (Style)FindResource("DangerButton");
            BtnConfirmApply.Content = "Saya Paham Risikonya — Terapkan Extreme Gaming";
        }
        else
        {
            PreviewWarningBox.Visibility = Visibility.Collapsed;
            BtnConfirmApply.Style = (Style)FindResource("PrimaryButton");
            BtnConfirmApply.Content = $"Terapkan {profile} Mode Sekarang";
        }

        if (!string.IsNullOrEmpty(preview.HardwareNotice))
        {
            PreviewHwNoticeBox.Visibility = Visibility.Visible;
            TxtPreviewHwNotice.Text = preview.HardwareNotice;
        }
        else
        {
            PreviewHwNoticeBox.Visibility = Visibility.Collapsed;
        }

        VerificationCard.Visibility = Visibility.Collapsed;
        PreviewCard.Visibility = Visibility.Visible;
        PreviewCard.BringIntoView();
    }

    private void BtnCancelPreview_Click(object sender, RoutedEventArgs e)
    {
        PreviewCard.Visibility = Visibility.Collapsed;
        _activePreviewProfile = null;
    }

    #endregion

    #region Apply & Verification (Steps 2, 4, 5: BACKUP, APPLY, VERIFY)

    private async void BtnConfirmApply_Click(object sender, RoutedEventArgs e)
    {
        if (_activePreviewProfile == null) return;
        var profile = _activePreviewProfile.Value;

        if (!AdministratorHelper.IsAdministrator)
        {
            MessageBox.Show(
                "Menerapkan profil performa memerlukan hak akses Administrator untuk mengonfigurasi layanan sistem, registry, dan kebijakan Windows Update.",
                "Hak Akses Administrator Diperlukan",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Additional confirmation dialog for Extreme Gaming
        if (profile == WindowsLiteProfileType.ExtremeGaming)
        {
            var confirm = MessageBox.Show(
                "EXTREME GAMING MODE\n\nMode ini akan menonaktifkan sebagian fungsi Windows yang tidak diperlukan untuk gaming (Windows Update, Spooler, Search Indexer, Hibernasi, Edge, OneDrive).\n\nMode ini tidak direkomendasikan untuk komputer kerja atau yang membutuhkan update otomatis.\n\nPastikan Anda siap menerapkan profil Extreme Gaming sekarang?",
                "Konfirmasi Extreme Gaming Mode",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;
        }
        else if (profile == WindowsLiteProfileType.LowEnd)
        {
            var confirm = MessageBox.Show(
                "LOW-END / POTATO MODE (MODE KENTANG)\n\nMode ini dirancang khusus untuk hardware berspesifikasi terbatas. Efek visual akan disetel ke Performa Terbaik (tanpa animasi dan transparansi), indeks Windows Search dimatikan untuk mencegah 100% disk usage, dan kompresi CompactOS diaktifkan.\n\nLanjutkan penerapan profil Low-End?",
                "Konfirmasi Low-End Mode",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;
        }

        PreviewCard.Visibility = Visibility.Collapsed;
        SetLoading(true, "Mempersiapkan penerapan profil...");

        try
        {
            // Step 2: Auto Backup (Restore Point)
            if (ChkAutoRestore.IsChecked == true)
            {
                SetLoading(true, $"Membuat System Restore Point (AturOS-Pre{profile})...");
                await _restoreService.CreateRestorePointAsync($"AturOS-Pre{profile}");
            }

            // Step 4: Apply Profile
            SetLoading(true, $"Menerapkan {profile} Mode ke sistem Windows...");
            var (success, msg) = await _liteService.ApplyProfileAsync(profile, p =>
            {
                Dispatcher.BeginInvoke(() => TxtProgress.Text = p);
            });

            // Step 5: Verification (Anti-Mock readback audit)
            SetLoading(true, "Melakukan audit verifikasi sub-sistem Windows (Anti-Mock)...");
            var verificationItems = await _liteService.VerifyProfileAsync(profile);
            ItemsVerification.ItemsSource = verificationItems;
            VerificationCard.Visibility = Visibility.Visible;
            VerificationCard.BringIntoView();

            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal menerapkan profil: {ex.Message}", isError: true);
        }
        finally
        {
            SetLoading(false);
            _activePreviewProfile = null;
        }
    }

    private void BtnCloseVerification_Click(object sender, RoutedEventArgs e)
    {
        VerificationCard.Visibility = Visibility.Collapsed;
    }

    private async void BtnRestartNow_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Restart komputer sekarang untuk menerapkan seluruh perubahan kernel, explorer, dan prioritas sistem secara optimal?",
            "Konfirmasi Restart",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            await ProcessHelper.RunCommandAsync("shutdown.exe", "/r /t 5 /c \"AturOS: Restart untuk menerapkan perubahan profil performa Windows.\"");
        }
    }

    #endregion

    #region Revert & Store Recovery (Step 6: ROLLBACK)

    private async void BtnRevert_Click(object sender, RoutedEventArgs e)
    {
        if (!AdministratorHelper.IsAdministrator)
        {
            MessageBox.Show(
                "Mengembalikan konfigurasi sistem ke standar membutuhkan hak Administrator.",
                "Hak Akses Diperlukan",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            "Kembalikan seluruh konfigurasi sistem ke Standar Windows (Revert)?\n\nLayanan sistem (SysMain, WSearch, Spooler, DiagTrack, Bluetooth) akan diaktifkan kembali, Windows Update dipulihkan, dan skema daya dikembalikan ke Balanced.",
            "Konfirmasi Revert Standar",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        PreviewCard.Visibility = Visibility.Collapsed;
        SetLoading(true, "Mengembalikan konfigurasi sistem ke standar...");

        try
        {
            if (ChkAutoRestore.IsChecked == true)
            {
                SetLoading(true, "Membuat System Restore Point sebelum rollback...");
                await _restoreService.CreateRestorePointAsync("AturOS-PreRevert");
            }

            var (success, msg) = await _liteService.RevertToStandardAsync(p =>
            {
                Dispatcher.BeginInvoke(() => TxtProgress.Text = p);
            });

            // Verify Standard
            var verificationItems = await _liteService.VerifyProfileAsync(WindowsLiteProfileType.Standard);
            ItemsVerification.ItemsSource = verificationItems;
            VerificationCard.Visibility = Visibility.Visible;
            VerificationCard.BringIntoView();

            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal mengembalikan pengaturan: {ex.Message}", isError: true);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void BtnRestoreStore_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Jalankan instalasi pemulihan Microsoft Store?\n\nWindows akan mendaftarkan ulang dan mereset komponen Store.",
            "Pulihkan Microsoft Store",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        SetLoading(true, "Memulihkan paket Microsoft Store...");
        try
        {
            var (success, msg) = await WindowsLiteService.RestoreMicrosoftStoreAsync(p =>
            {
                Dispatcher.BeginInvoke(() => TxtProgress.Text = p);
            });
            ShowBanner(msg, isError: !success);
        }
        catch (Exception ex)
        {
            ShowBanner($"Gagal memulihkan Store: {ex.Message}", isError: true);
        }
        finally
        {
            SetLoading(false);
        }
    }

    #endregion

    #region UI State Helpers

    private void SetLoading(bool isLoading, string text = "")
    {
        ProgressCard.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        if (isLoading) TxtProgress.Text = text;
    }

    private async void ShowBanner(string message, bool isError)
    {
        _bannerCts?.Cancel();
        var cts = new CancellationTokenSource();
        _bannerCts = cts;

        TxtBanner.Text = message;
        StatusBanner.Background = isError
            ? (System.Windows.Media.Brush)FindResource("BrushDangerLight")
            : (System.Windows.Media.Brush)FindResource("BrushSuccessLight");
        StatusBanner.BorderBrush = isError
            ? (System.Windows.Media.Brush)FindResource("BrushDanger")
            : (System.Windows.Media.Brush)FindResource("BrushSuccess");
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

    #endregion
}
