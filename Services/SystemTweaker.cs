using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AturOS.Models;

namespace AturOS.Services;

/// <summary>
/// Consolidated Architectural Facade implementing and delegating execution
/// for all 12 modules in accordance with Diagnosa.md.
/// </summary>
public class SystemTweaker
{
    private static readonly Lazy<SystemTweaker> _instance = new(() => new SystemTweaker());
    public static SystemTweaker Instance => _instance.Value;

    // Service providers
    public SystemInfoService SystemInfo => new();
    public PerformanceProfileService PerformanceProfile => new();
    public WindowsLiteService WindowsLite => new();
    public AppDebloaterService AppDebloater => new();
    public StorageCleanerService StorageCleaner => new();
    public HardwareTuningService HardwareTuning => new();
    public MemoryOptimizerService MemoryOptimizer => new();
    public WindowsUpdateControlService WindowsUpdate => new();
    public SystemDoctorService SystemDoctor => SystemDoctorService.Instance;
    public DnsOptimizerService DnsOptimizer => new();
    public ExplorerTweaksService ExplorerTweaks => new();
    public WingetInstallerService WingetInstaller => new();
    public EmergencyToolsService EmergencyTools => new();
    public RestorePointService RestorePoint => new();
    public RegistryBackupService RegistryBackup => new();

    #region Facade Quick Actions

    /// <summary>
    /// Modul 1: One-Click Quick Health Optimizer
    /// Membersihkan Working Set RAM, memindai dan membersihkan file sampah temporer.
    /// </summary>
    public async Task<(bool Success, string Message)> RunQuickHealthOptimizerAsync(Action<string>? progress = null)
    {
        try
        {
            progress?.Invoke("Memulai optimasi cepat sistem...");

            // 1. Working set trim
            progress?.Invoke("Mengoptimalkan working set RAM...");
            var memReport = await SystemOptimizer.Instance.OptimizeWorkingSetAsync(progress);

            // 2. Storage clean (TEMP folders)
            progress?.Invoke("Membersihkan cache file temporer...");
            var targets = StorageCleaner.GetDefaultTargets();
            var cleanTargets = targets.Where(t => t.Id == "user_temp" || t.Id == "win_temp").ToList();
            foreach (var target in cleanTargets)
            {
                target.IsSelected = true;
                await StorageCleaner.ScanTargetAsync(target);
            }

            var cleanRes = await StorageCleaner.CleanTargetsAsync(cleanTargets, progress);
            long totalFreed = cleanRes.BytesFreed;

            string freedMb = $"{totalFreed / (1024.0 * 1024.0):F1} MB";
            progress?.Invoke($"Optimasi selesai! RAM di-trim pada {memReport.ProcessedProcesses} proses, membebaskan {freedMb} disk.");
            return (true, $"Optimasi berhasil. {memReport.ProcessedProcesses} proses di-trim, {freedMb} ruang penyimpanan dibebaskan.");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Quick Health Optimizer gagal: {ex.Message}");
            return (false, $"Terjadi kesalahan saat optimasi: {ex.Message}");
        }
    }

    /// <summary>
    /// Modul 1: 1-Click Profile Switcher (Daily, Work, Gaming)
    /// </summary>
    public async Task<(bool Success, string Message)> SwitchPerformanceProfileAsync(PerformanceProfileMode mode)
    {
        return await PerformanceProfile.ApplyProfileAsync(mode);
    }

    /// <summary>
    /// Modul 7: System Doctor - Scan & Auto-repair SFC & DISM
    /// </summary>
    public async Task<(bool Success, string Message, bool DismExecuted)> ScanAndRepairSystemFilesAsync(Action<string>? progress = null)
    {
        return await SystemDoctor.ScanAndRepairSystemFilesAsync(progress);
    }

    /// <summary>
    /// Modul 11 & Header Quick Action: Restart Explorer
    /// </summary>
    public async Task<(bool Success, string Message)> RestartExplorerAsync()
    {
        bool ok = await EmergencyTools.RestartExplorerAsync();
        return (ok, ok ? "Windows Explorer berhasil dimuat ulang." : "Gagal memuat ulang Windows Explorer.");
    }

    /// <summary>
    /// Modul 11 & Header Quick Action: Kill Not-Responding Tasks
    /// </summary>
    public async Task<(bool Success, string Message)> KillHangingTasksAsync()
    {
        return await EmergencyTools.KillNotRespondingTasksAsync();
    }

    /// <summary>
    /// Modul 12 & Header Quick Action: Create System Restore Point
    /// </summary>
    public async Task<(bool Success, string Message)> CreateSystemRestorePointAsync(string description = "AturOS-Checkpoint")
    {
        return await RestorePoint.CreateRestorePointAsync(description);
    }

    #endregion
}
