using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text.Json;
using System.Threading.Tasks;
using AturOS.Helpers;
using AturOS.Models;
using Microsoft.Win32;

namespace AturOS.Services;

public class SystemSnapshotData
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Tag { get; set; } = "Snapshot";
    public string WindowsVersion { get; set; } = string.Empty;
    public string PowerPlanGuid { get; set; } = string.Empty;

    public Dictionary<string, int> RegistryDwords { get; set; } = new();
    public Dictionary<string, string> RegistryStrings { get; set; } = new();
    public Dictionary<string, string> ServiceStartTypes { get; set; } = new();
    public Dictionary<string, string> ServiceStates { get; set; } = new();
}

public class SnapshotManagerService
{
    private static readonly string SnapshotDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AturOS", "Snapshots");

    public SnapshotManagerService()
    {
        if (!Directory.Exists(SnapshotDir))
        {
            Directory.CreateDirectory(SnapshotDir);
        }
    }

    public async Task<(bool Success, string FilePath)> CreateSnapshotAsync(string tag = "PreTweak")
    {
        return await Task.Run(() =>
        {
            try
            {
                var snapshot = new SystemSnapshotData
                {
                    Tag = tag,
                    WindowsVersion = Environment.OSVersion.VersionString
                };

                // 1. Capture Power Plan
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes");
                    snapshot.PowerPlanGuid = key?.GetValue("ActivePowerScheme")?.ToString() ?? string.Empty;
                }
                catch { }

                // 2. Capture Registry Values
                CaptureRegistryDword(snapshot, RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation");
                CaptureRegistryDword(snapshot, RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff");
                CaptureRegistryDword(snapshot, RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode");
                CaptureRegistryDword(snapshot, RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "DisablePagingExecutive");
                CaptureRegistryDword(snapshot, RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "AUOptions");
                CaptureRegistryDword(snapshot, RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled");
                CaptureRegistryDword(snapshot, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency");
                CaptureRegistryDword(snapshot, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting");
                CaptureRegistryDword(snapshot, RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled");

                CaptureRegistryString(snapshot, RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed");
                CaptureRegistryString(snapshot, RegistryHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", "MinAnimate");

                // 3. Capture Services Status
                var servicesToCapture = new[] { "SysMain", "WSearch", "Spooler", "DiagTrack", "WerSvc", "Fax", "RemoteRegistry", "bthserv" };
                foreach (var svcName in servicesToCapture)
                {
                    try
                    {
                        using var sc = new ServiceController(svcName);
                        snapshot.ServiceStates[svcName] = sc.Status.ToString();

                        using var svcKey = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{svcName}");
                        var startVal = svcKey?.GetValue("Start");
                        if (startVal != null)
                        {
                            snapshot.ServiceStartTypes[svcName] = startVal.ToString()!;
                        }
                    }
                    catch
                    {
                        // Service may not exist on this Windows build
                    }
                }

                // Save JSON
                var safeTag = new string(tag.Where(char.IsLetterOrDigit).ToArray());
                if (string.IsNullOrEmpty(safeTag)) safeTag = "Snapshot";
                var fileName = $"Snapshot_{safeTag}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var fullPath = Path.Combine(SnapshotDir, fileName);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(snapshot, options);
                File.WriteAllText(fullPath, json);

                LoggerService.Instance.Success($"Snapshot sistem berhasil disimpan di: {fullPath}");
                return (true, fullPath);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Gagal membuat snapshot sistem: {ex.Message}");
                return (false, ex.Message);
            }
        });
    }

    public async Task<(bool Success, string Message)> RestoreSnapshotAsync(string filePath, Action<string>? progress = null)
    {
        return await Task.Run(async () =>
        {
            if (!File.Exists(filePath)) return (false, "File snapshot JSON tidak ditemukan.");

            try
            {
                progress?.Invoke("Membaca file snapshot sistem...");
                var json = File.ReadAllText(filePath);
                var snapshot = JsonSerializer.Deserialize<SystemSnapshotData>(json);
                if (snapshot == null) return (false, "Data snapshot kosong atau tidak valid.");

                // 1. Restore Registry DWORDs
                progress?.Invoke("Memulihkan nilai registri sistem...");
                foreach (var (keyPath, val) in snapshot.RegistryDwords)
                {
                    var (hive, subKey, valueName) = ParseKeyPath(keyPath);
                    RegistryHelper.SetDWord(hive, subKey, valueName, val);
                }

                // 2. Restore Registry Strings
                foreach (var (keyPath, val) in snapshot.RegistryStrings)
                {
                    var (hive, subKey, valueName) = ParseKeyPath(keyPath);
                    RegistryHelper.SetString(hive, subKey, valueName, val);
                }

                // 3. Restore Services
                progress?.Invoke("Mengembalikan kondisi layanan Windows...");
                foreach (var (svcName, startType) in snapshot.ServiceStartTypes)
                {
                    string startArg = startType switch
                    {
                        "2" => "auto",
                        "3" => "demand",
                        "4" => "disabled",
                        _ => "demand"
                    };
                    await ProcessHelper.RunCommandAsync("sc.exe", $"config {svcName} start={startArg}", timeoutMs: 5000);
                }

                foreach (var (svcName, state) in snapshot.ServiceStates)
                {
                    if (state.Equals("Running", StringComparison.OrdinalIgnoreCase))
                    {
                        await ProcessHelper.RunCommandAsync("sc.exe", $"start {svcName}", timeoutMs: 5000);
                    }
                    else if (state.Equals("Stopped", StringComparison.OrdinalIgnoreCase))
                    {
                        await ProcessHelper.RunCommandAsync("sc.exe", $"stop {svcName}", timeoutMs: 5000);
                    }
                }

                // 4. Restore Power Plan
                if (!string.IsNullOrEmpty(snapshot.PowerPlanGuid))
                {
                    progress?.Invoke("Mengembalikan skema daya Windows...");
                    await ProcessHelper.RunCommandAsync("powercfg.exe", $"/setactive {snapshot.PowerPlanGuid}", timeoutMs: 5000);
                }

                LoggerService.Instance.Success($"Snapshot sistem dari {snapshot.CreatedAt:dd-MM-yyyy HH:mm} berhasil dipulihkan.");
                return (true, $"Snapshot sistem ({snapshot.Tag} • {snapshot.CreatedAt:dd/MM/yyyy HH:mm}) berhasil dipulihkan secara penuh.");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Gagal memulihkan snapshot: {ex.Message}");
                return (false, $"Gagal memulihkan snapshot: {ex.Message}");
            }
        });
    }

    public List<string> GetAvailableSnapshotFiles()
    {
        try
        {
            if (!Directory.Exists(SnapshotDir)) return new List<string>();
            return Directory.GetFiles(SnapshotDir, "Snapshot_*.json")
                            .OrderByDescending(f => File.GetCreationTime(f))
                            .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static void CaptureRegistryDword(SystemSnapshotData snapshot, RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey);
            var val = key?.GetValue(valueName);
            if (val is int i)
            {
                snapshot.RegistryDwords[$"{hive}\\{subKey}\\{valueName}"] = i;
            }
        }
        catch { }
    }

    private static void CaptureRegistryString(SystemSnapshotData snapshot, RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey);
            var val = key?.GetValue(valueName)?.ToString();
            if (val != null)
            {
                snapshot.RegistryStrings[$"{hive}\\{subKey}\\{valueName}"] = val;
            }
        }
        catch { }
    }

    private static (RegistryHive Hive, string SubKey, string ValueName) ParseKeyPath(string fullKeyPath)
    {
        var firstSlash = fullKeyPath.IndexOf('\\');
        var lastSlash = fullKeyPath.LastIndexOf('\\');

        var hiveStr = fullKeyPath[..firstSlash];
        var subKey = fullKeyPath.Substring(firstSlash + 1, lastSlash - firstSlash - 1);
        var valName = fullKeyPath[(lastSlash + 1)..];

        var hive = hiveStr.Contains("CurrentUser", StringComparison.OrdinalIgnoreCase)
            ? RegistryHive.CurrentUser
            : RegistryHive.LocalMachine;

        return (hive, subKey, valName);
    }
}
