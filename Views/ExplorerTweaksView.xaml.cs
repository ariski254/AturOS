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
        var (s, m) = await _explorerService.SetShowFileExtensionsAsync(true);
        ShowBanner(m, isError: !s);
    }

    private async void BtnHideExt_Click(object sender, RoutedEventArgs e)
    {
        var (s, m) = await _explorerService.SetShowFileExtensionsAsync(false);
        ShowBanner(m, isError: !s);
    }

    private async void BtnShowHidden_Click(object sender, RoutedEventArgs e)
    {
        var (s, m) = await _explorerService.SetShowHiddenFilesAsync(true);
        ShowBanner(m, isError: !s);
    }

    private async void BtnHideHidden_Click(object sender, RoutedEventArgs e)
    {
        var (s, m) = await _explorerService.SetShowHiddenFilesAsync(false);
        ShowBanner(m, isError: !s);
    }

    private async void BtnRemoveShortcut_Click(object sender, RoutedEventArgs e)
    {
        var (s, m) = await _explorerService.SetRemoveShortcutSuffixAsync(true);
        ShowBanner(m, isError: !s);
    }

    private async void BtnRestoreShortcut_Click(object sender, RoutedEventArgs e)
    {
        var (s, m) = await _explorerService.SetRemoveShortcutSuffixAsync(false);
        ShowBanner(m, isError: !s);
    }

    private async void BtnAddTakeOwnership_Click(object sender, RoutedEventArgs e)
    {
        var (s, m) = await _explorerService.SetTakeOwnershipAsync(true);
        ShowBanner(m, isError: !s);
    }

    private async void BtnRemoveTakeOwnership_Click(object sender, RoutedEventArgs e)
    {
        var (s, m) = await _explorerService.SetTakeOwnershipAsync(false);
        ShowBanner(m, isError: !s);
    }

    private async void BtnDisableWarning_Click(object sender, RoutedEventArgs e)
    {
        var (s, m) = await _explorerService.SetDisableSecurityWarningAsync(true);
        ShowBanner(m, isError: !s);
    }

    private async void BtnEnableWarning_Click(object sender, RoutedEventArgs e)
    {
        var (s, m) = await _explorerService.SetDisableSecurityWarningAsync(false);
        ShowBanner(m, isError: !s);
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
