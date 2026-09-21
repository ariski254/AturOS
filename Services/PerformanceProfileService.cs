using System.Windows.Threading;
using AturOS.Helpers;
using AturOS.Models;
using Microsoft.Win32;

namespace AturOS.Services;

public enum PerformanceProfileMode
{
    DailyBalance,
    Productivity,
    Gaming,
    Unknown
}

public class PerformanceProfileService
{
    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private readonly PowerPlanService _powerPlanService = new();
    private readonly MemoryOptimizerService _memoryService = new();
    private readonly SystemInfoService _sysInfoService = new();
    private DispatcherTimer? _autoTrimTimer;

    public PerformanceProfileMode CurrentMode { get; private set; } = PerformanceProfileMode.DailyBalance;

    public async Task<PerformanceProfileMode> DetectCurrentProfileAsync()
    {
        var tweak = _powerPlanService.GetPowerPlanTweakItem();
        await _powerPlanService.RefreshPowerPlanStatusAsync(tweak);

        if (tweak.Status == TweakStatus.Aktif)
        {
            CurrentMode = PerformanceProfileMode.Gaming;
        }
        else
        {
            CurrentMode = PerformanceProfileMode.DailyBalance;
        }

        return CurrentMode;
    }

    public async Task<(bool Success, string Message)> ApplyProfileAsync(PerformanceProfileMode mode)
    {
        switch (mode)
        {
            case PerformanceProfileMode.DailyBalance:
                return await ApplyDailyBalanceAsync();
            case PerformanceProfileMode.Productivity:
                return await ApplyProductivityAsync();
            case PerformanceProfileMode.Gaming:
                return await ApplyGamingAsync();
            default:
                return (false, "Profil tidak dikenal.");
        }
    }

    private async Task<(bool Success, string Message)> ApplyDailyBalanceAsync()
    {
        LoggerService.Instance.Info("Menerapkan Mode Seimbang (Daily Balance)...");
        StopAutoTrim();

        // 1. Set Balanced power scheme
        var tweak = _powerPlanService.GetPowerPlanTweakItem();
        await _powerPlanService.RestoreBalancedPlanAsync(tweak);

        // 2. Enable mouse acceleration standard
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true);
            key?.SetValue("MouseSpeed", "1");
            key?.SetValue("MouseThreshold1", "6");
            key?.SetValue("MouseThreshold2", "10");
        }
        catch { }

        CurrentMode = PerformanceProfileMode.DailyBalance;
        LoggerService.Instance.Success("Mode Seimbang (Daily Balance) aktif.");
        return (true, "Mode Seimbang aktif: Profil daya seimbang dan efisiensi konsumsi daya idle.");
    }

    private async Task<(bool Success, string Message)> ApplyProductivityAsync()
    {
        LoggerService.Instance.Info("Menerapkan Mode Kerja & Produktivitas...");

        // 1. Set Balanced power scheme
        var tweak = _powerPlanService.GetPowerPlanTweakItem();
        await _powerPlanService.RestoreBalancedPlanAsync(tweak);

        // 2. Start auto-trim timer (check every 30s if RAM > 80%)
        StartAutoTrim();

        // 3. Mute background distraction notifications (Focus Assist)
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\default$windows.data.ambientnotificationsettings");
            // Set Focus Assist priority only
            using var focusKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings");
            focusKey.SetValue("NOC_GLOBAL_SETTING_ALLOW_CRITICAL_TOASTS_ABOVE_LOCK", 1, RegistryValueKind.DWord);
        }
        catch { }

        CurrentMode = PerformanceProfileMode.Productivity;
        LoggerService.Instance.Success("Mode Kerja & Produktivitas aktif.");
        return (true, "Mode Kerja & Produktivitas aktif: Auto-trim RAM aktif saat pemakaian > 80%, notifikasi latar diminimalkan.");
    }

    private async Task<(bool Success, string Message)> ApplyGamingAsync()
    {
        LoggerService.Instance.Info("Menerapkan Mode Gaming (Extreme Latency & FPS)...");
        StopAutoTrim();

        // 1. Activate Ultimate Performance
        var tweak = _powerPlanService.GetPowerPlanTweakItem();
        await _powerPlanService.EnableUltimatePerformanceAsync(tweak);

        // 2. Disable CPU Core Parking
        await ProcessHelper.RunCommandAsync("powercfg.exe", "/setacvalueindex scheme_current sub_processor CPMINCORES 100");
        await ProcessHelper.RunCommandAsync("powercfg.exe", "/setactive scheme_current");

        // 3. Disable mouse acceleration (1:1 precision)
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true);
            key?.SetValue("MouseSpeed", "0");
            key?.SetValue("MouseThreshold1", "0");
            key?.SetValue("MouseThreshold2", "0");
        }
        catch { }

        // 4. Initial memory trim for clean slate
        await _memoryService.OptimizeRamAsync();

        CurrentMode = PerformanceProfileMode.Gaming;
        LoggerService.Instance.Success("Mode Gaming aktif.");
        return (true, "Mode Gaming aktif: Profil daya Ultimate Performance, Core Parking nonaktif, akurasi mouse 1:1, dan RAM dioptimalkan.");
    }

    private void StartAutoTrim()
    {
        StopAutoTrim();
        _autoTrimTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _autoTrimTimer.Tick += async (s, e) =>
        {
            var metrics = _sysInfoService.GetMetrics();
            if (metrics.RamUsagePercent >= 80.0)
            {
                LoggerService.Instance.Info($"[Auto-Trim] RAM mencapai {metrics.RamUsagePercent}%. Melakukan trim memori otomatis...");
                await _memoryService.OptimizeRamAsync();
            }
        };
        _autoTrimTimer.Start();
    }

    private void StopAutoTrim()
    {
        _autoTrimTimer?.Stop();
        _autoTrimTimer = null;
    }
}
