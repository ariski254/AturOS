using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AturOS.Helpers;
using AturOS.Services;
using AturOS.Views;

namespace AturOS;

public partial class MainWindow : Window
{
    private readonly Lazy<DashboardView> _dashboardView = new(() => new DashboardView());
    private readonly Lazy<WindowsLiteView> _liteView = new(() => new WindowsLiteView());
    private readonly Lazy<AppDebloaterView> _debloaterView = new(() => new AppDebloaterView());
    private readonly Lazy<StorageCleanerView> _cleanerView = new(() => new StorageCleanerView());
    private readonly Lazy<HardwareGamingView> _hardwareView = new(() => new HardwareGamingView());
    private readonly Lazy<MemoryOptimizerView> _memoryView = new(() => new MemoryOptimizerView());
    private readonly Lazy<WindowsUpdateView> _updateView = new(() => new WindowsUpdateView());
    private readonly Lazy<SystemDoctorView> _doctorView = new(() => new SystemDoctorView());
    private readonly Lazy<NetworkDnsView> _networkView = new(() => new NetworkDnsView());
    private readonly Lazy<ExplorerTweaksView> _explorerView = new(() => new ExplorerTweaksView());
    private readonly Lazy<WingetInstallerView> _wingetView = new(() => new WingetInstallerView());
    private readonly Lazy<EmergencyToolsView> _emergencyView = new(() => new EmergencyToolsView());
    private readonly Lazy<BackupRestoreView> _backupView = new(() => new BackupRestoreView());

    public MainWindow()
    {
        InitializeComponent();

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeEnvironment();
        NavigateTo("Dashboard");
    }

    private void InitializeEnvironment()
    {
        // 1. Detect OS & Build
        var (osName, osBuild) = SystemInfoService.GetOperatingSystemDetails();
        string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? (Environment.Is64BitOperatingSystem ? "x64" : "x86");
        TxtFooterOs.Text = $"{osName}";
        TxtFooterBuild.Text = $"{osBuild} • {arch}";

        // 2. Administrator Status
        bool isAdmin = AdministratorHelper.IsAdministrator;
        if (isAdmin)
        {
            TxtFooterAdmin.Text = "Administrator";
            TxtFooterAdmin.Foreground = (Brush)FindResource("BrushSuccess");
            BadgeAdmin.Background = (Brush)FindResource("BrushSuccessLight");
            IconAdminPath.Fill = (Brush)FindResource("BrushSuccess");
            BtnElevate.Visibility = Visibility.Collapsed;
        }
        else
        {
            TxtFooterAdmin.Text = "User Standar";
            TxtFooterAdmin.Foreground = (Brush)FindResource("BrushWarning");
            BadgeAdmin.Background = (Brush)FindResource("BrushWarningLight");
            IconAdminPath.Fill = (Brush)FindResource("BrushWarning");
            BtnElevate.Visibility = Visibility.Visible;
        }

        LoggerService.Instance.Info($"AturOS dimulai pada {osName} {osBuild} ({arch}), Admin: {isAdmin}");
    }

    private string _currentDestination = "";

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string destination)
        {
            if (_currentDestination == destination && MainContentHost.Content != null) return;
            NavigateTo(destination);
        }
    }

    private void NavigateTo(string destination)
    {
        if (_currentDestination == destination && MainContentHost.Content != null)
        {
            return;
        }

        try
        {
            object targetView = destination switch
            {
                "Dashboard" => _dashboardView.Value,
                "Lite" => _liteView.Value,
                "Debloater" => _debloaterView.Value,
                "Cleaner" => _cleanerView.Value,
                "Hardware" => _hardwareView.Value,
                "Memory" => _memoryView.Value,
                "Update" => _updateView.Value,
                "Doctor" => _doctorView.Value,
                "Network" => _networkView.Value,
                "Explorer" => _explorerView.Value,
                "Winget" => _wingetView.Value,
                "Emergency" => _emergencyView.Value,
                "Backup" => _backupView.Value,
                _ => _dashboardView.Value
            };

            if (MainContentHost.Content == targetView)
            {
                _currentDestination = destination;
                return;
            }

            _currentDestination = destination;
            MainContentHost.Content = targetView;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal beralih ke menu '{destination}': {ex.Message}\n{ex}");
            MessageBox.Show(
                $"Gagal memuat halaman menu '{destination}':\n{ex.Message}\n\nOperasi diamankan.",
                "Navigasi Menu",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    #region Search Bar Logic

    private void TxtGlobalSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        TxtSearchPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void TxtGlobalSearch_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtGlobalSearch.Text))
        {
            TxtSearchPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void TxtGlobalSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtGlobalSearch.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void TxtGlobalSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteSearchJump(TxtGlobalSearch.Text.Trim().ToLowerInvariant());
        }
    }

    private void ExecuteSearchJump(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        if (query.Contains("doc") || query.Contains("sfc") || query.Contains("dism") || query.Contains("rusak") || query.Contains("repair") || query.Contains("perbaik") || query.Contains("chkdsk") || query.Contains("dirty"))
        {
            NavDoctor.IsChecked = true;
        }
        else if (query.Contains("performa") || query.Contains("profil") || query.Contains("mode") || query.Contains("lite") || query.Contains("level") || query.Contains("edge") || query.Contains("barebone") || query.Contains("pangkas"))
        {
            NavLite.IsChecked = true;
        }
        else if (query.Contains("uninstall") || query.Contains("uninstaller") || query.Contains("paksa") || query.Contains("debloat") || query.Contains("uwp") || query.Contains("copot") || query.Contains("app"))
        {
            NavDebloater.IsChecked = true;
        }
        else if (query.Contains("penyimpanan") || query.Contains("clean") || query.Contains("sampah") || query.Contains("temp") || query.Contains("storage") || query.Contains("drive") || query.Contains("hiber") || query.Contains("compact") || query.Contains("sense") || query.Contains("reserved") || query.Contains("access"))
        {
            NavCleaner.IsChecked = true;
        }
        else if (query.Contains("game") || query.Contains("gaming") || query.Contains("fps") || query.Contains("gpu") || query.Contains("hags") || query.Contains("hard") || query.Contains("core") || query.Contains("tick") || query.Contains("ultimate") || query.Contains("power") || query.Contains("priority") || query.Contains("throttl") || query.Contains("delay"))
        {
            NavHardware.IsChecked = true;
        }
        else if (query.Contains("ram") || query.Contains("memori") || query.Contains("mem") || query.Contains("working set") || query.Contains("standby") || query.Contains("pagefile") || query.Contains("sysmain") || query.Contains("compress") || query.Contains("paging"))
        {
            NavMemory.IsChecked = true;
        }
        else if (query.Contains("update") || query.Contains("lockdown") || query.Contains("patch") || query.Contains("2099"))
        {
            NavUpdate.IsChecked = true;
        }
        else if (query.Contains("dns") || query.Contains("ping") || query.Contains("cloudflare") || query.Contains("net") || query.Contains("ip"))
        {
            NavNetwork.IsChecked = true;
        }
        else if (query.Contains("explor") || query.Contains("menu") || query.Contains("context") || query.Contains("folder") || query.Contains("hidden") || query.Contains("shortcut"))
        {
            NavExplorer.IsChecked = true;
        }
        else if (query.Contains("winget") || query.Contains("pasang") || query.Contains("install") || query.Contains("aplikasi"))
        {
            NavWinget.IsChecked = true;
        }
        else if (query.Contains("darurat") || query.Contains("task") || query.Contains("baterai") || query.Contains("emergen") || query.Contains("hang") || query.Contains("macet"))
        {
            NavEmergency.IsChecked = true;
        }
        else if (query.Contains("back") || query.Contains("restor") || query.Contains("point") || query.Contains("cadang") || query.Contains("reg"))
        {
            NavBackup.IsChecked = true;
        }
        else
        {
            NavDashboard.IsChecked = true;
        }
    }

    #endregion

    #region Header Quick Actions

    private async void BtnHeaderRestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Restart Windows Explorer sekarang?\n\nTaskbar dan desktop akan dimuat ulang sejenak untuk menerapkan perubahan sistem.",
            "Restart Explorer",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnHeaderRestartExplorer.IsEnabled = false;
        try
        {
            var (success, msg) = await SystemTweaker.Instance.RestartExplorerAsync();
            MessageBox.Show(msg, "Restart Explorer", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        finally
        {
            BtnHeaderRestartExplorer.IsEnabled = true;
        }
    }

    private async void BtnHeaderKillTasks_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Tutup paksa seluruh proses dan aplikasi yang sedang macet (Not Responding)?",
            "Tutup Aplikasi Macet",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnHeaderKillTasks.IsEnabled = false;
        try
        {
            var (success, msg) = await SystemTweaker.Instance.KillHangingTasksAsync();
            MessageBox.Show(msg, "Status Task Macet", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        finally
        {
            BtnHeaderKillTasks.IsEnabled = true;
        }
    }

    private async void BtnHeaderRestorePoint_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Buat System Restore Point baru sekarang?\n\nRestore point memungkinkan pengembalian konfigurasi Windows jika terjadi kendala.",
            "Buat Restore Point",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnHeaderRestorePoint.IsEnabled = false;
        try
        {
            var (success, msg) = await SystemTweaker.Instance.CreateSystemRestorePointAsync("AturOS-HeaderCheckpoint");
            MessageBox.Show(msg, "System Restore Point", MessageBoxButton.OK, success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        finally
        {
            BtnHeaderRestorePoint.IsEnabled = true;
        }
    }

    #endregion

    private void BtnElevate_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Jalankan ulang AturOS dengan hak akses Administrator?\n\nJendela aplikasi saat ini akan ditutup dan dibuka kembali dengan elevasi UAC.",
            "Elevasi Administrator",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            AdministratorHelper.RestartAsAdministrator();
        }
    }
}
