using System.Windows;
using System.Windows.Controls;
using AturOS.Services;

namespace AturOS.Views;

public partial class ExplorerTweaksView : UserControl
{
    private readonly ExplorerTweaksService _explorerService = new();

    public ExplorerTweaksView()
    {
        InitializeComponent();
    }

    private async void BtnShowExt_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetShowFileExtensionsAsync(true);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnHideExt_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetShowFileExtensionsAsync(false);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnShowHidden_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetShowHiddenFilesAsync(true);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnHideHidden_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetShowHiddenFilesAsync(false);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnRemoveShortcut_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetRemoveShortcutSuffixAsync(true);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnRestoreShortcut_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetRemoveShortcutSuffixAsync(false);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnAddTakeOwnership_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetTakeOwnershipAsync(true);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnRemoveTakeOwnership_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetTakeOwnershipAsync(false);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnDisableWarning_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetDisableSecurityWarningAsync(true);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnEnableWarning_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetDisableSecurityWarningAsync(false);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnClassicMenuOn_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetClassicContextMenuAsync(true);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnClassicMenuOff_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetClassicContextMenuAsync(false);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnTerminalAdminOn_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetOpenTerminalAdminAsync(true);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnTerminalAdminOff_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetOpenTerminalAdminAsync(false);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnDisableBingOn_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetDisableBingSearchAsync(true);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnDisableBingOff_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn != null) btn.IsEnabled = false;
        try
        {
            var (s, m) = await _explorerService.SetDisableBingSearchAsync(false);
            ShowBanner(m, isError: !s);
        }
        finally
        {
            if (btn != null) btn.IsEnabled = true;
        }
    }

    private async void BtnRestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Restart Windows Explorer sekarang untuk menerapkan perubahan tampilan?",
            "Konfirmasi Restart Explorer",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        bool ok = await RegistryTweakService.RestartExplorerAsync();
        ShowBanner(ok ? "Windows Explorer berhasil direstart." : "Gagal merestart Explorer.", isError: !ok);
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
