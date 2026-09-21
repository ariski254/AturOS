using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AturOS.Helpers;
using AturOS.Models;
using Microsoft.Win32;

namespace AturOS.Services;

public class SystemDoctorService
{
    private static readonly Lazy<SystemDoctorService> _instance = new(() => new SystemDoctorService());
    public static SystemDoctorService Instance => _instance.Value;

    #region 1. System File Integrity (SFC & DISM Auto-Repair)

    public async Task<(bool Success, string Message, bool DismExecuted)> ScanAndRepairSystemFilesAsync(Action<string>? progress = null)
    {
        progress?.Invoke("Memulai pemindaian integritas berkas sistem (sfc /scannow)...");

        try
        {
            var sfcResult = await ProcessHelper.RunProcessAsync("sfc.exe", "/scannow");
            string sfcOutput = sfcResult.StandardOutput;

            if (sfcResult.ExitCode == 0 || sfcOutput.Contains("did not find any integrity violations", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Invoke("SFC: Sistem dalam kondisi sehat. Tidak ditemukan pelanggaran integritas berkas.");
                return (true, "Integritas berkas sistem sempurna. Tidak ditemukan korupsi file Windows.", false);
            }

            if (sfcOutput.Contains("found corrupt files and successfully repaired them", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Invoke("SFC: Berkas korup berhasil diperbaiki secara otomatis oleh Windows Resource Protection.");
                return (true, "SFC mendeteksi berkas rusak dan berhasil memperbaikinya secara otomatis.", false);
            }

            // Kerusakan terdeteksi yang gagal diperbaiki oleh SFC -> Otomatis eksekusi DISM RestoreHealth
            progress?.Invoke("SFC mendeteksi kerusakan yang belum selesai. Menjalankan DISM /Online /Cleanup-Image /RestoreHealth...");
            var dismResult = await ProcessHelper.RunProcessAsync("dism.exe", "/Online /Cleanup-Image /RestoreHealth");

            bool dismSuccess = dismResult.ExitCode == 0 ||
                               dismResult.StandardOutput.Contains("The restore operation completed successfully", StringComparison.OrdinalIgnoreCase);

            if (dismSuccess)
            {
                progress?.Invoke("DISM: Pemulihan citra komponen Windows berhasil diselesaikan.");
                return (true, "DISM RestoreHealth berhasil memulihkan komponen sistem Windows yang rusak.", true);
            }
            else
            {
                progress?.Invoke($"DISM selesai dengan kode {dismResult.ExitCode}.");
                return (false, $"SFC & DISM selesai. Detail: {dismResult.StandardOutput.Trim()}", true);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Pengecekan integritas sistem gagal: {ex.Message}");
            return (false, $"Terjadi kesalahan eksekusi: {ex.Message}", false);
        }
    }

    #endregion

    #region 2. Windows Update Queue Troubleshooter

    public async Task<(bool Success, string Message)> RepairWindowsUpdateQueueAsync(Action<string>? progress = null)
    {
        progress?.Invoke("Menghentikan layanan Windows Update & BITS...");

        string script = @"
            $services = @('wuauserv', 'cryptSvc', 'bits', 'msiserver')
            foreach ($s in $services) {
                Stop-Service -Name $s -Force -ErrorAction SilentlyContinue
            }

            $windir = [Environment]::GetFolderPath('Windows')
            $sd = Join-Path $windir 'SoftwareDistribution'
            $cat = Join-Path $windir 'System32\catroot2'

            # Hapus folder backup lama jika ada
            if (Test-Path ""$sd.old"") { Remove-Item -Path ""$sd.old"" -Recurse -Force -ErrorAction SilentlyContinue }
            if (Test-Path ""$cat.old"") { Remove-Item -Path ""$cat.old"" -Recurse -Force -ErrorAction SilentlyContinue }

            # Rename folder cache antrian update
            try { Rename-Item -Path $sd -NewName 'SoftwareDistribution.old' -ErrorAction SilentlyContinue } catch {}
            try { Rename-Item -Path $cat -NewName 'catroot2.old' -ErrorAction SilentlyContinue } catch {}

            # Jika rename gagal karena file lock, bersihkan isi folder Download & DataStore
            $downloadPath = Join-Path $sd 'Download'
            if (Test-Path $downloadPath) {
                Get-ChildItem -Path $downloadPath -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
            }

            # Nyalakan kembali layanan
            foreach ($s in $services) {
                Start-Service -Name $s -ErrorAction SilentlyContinue
            }
        ";

        try
        {
            progress?.Invoke("Mereset folder antrian SoftwareDistribution dan Catroot2...");
            var result = await ProcessHelper.RunPowerShellScriptAsync(script);

            progress?.Invoke("Menghidupkan kembali layanan Windows Update...");
            return (true, "Antrian Windows Update berhasil direset dan layanan sistem telah direstart.");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Reset antrian Windows Update gagal: {ex.Message}");
            return (false, $"Gagal mereset antrian update: {ex.Message}");
        }
    }

    #endregion

    #region 3. Network Stack Repair

    public async Task<(bool Success, string Message)> RepairNetworkStackAsync(Action<string>? progress = null)
    {
        try
        {
            progress?.Invoke("Mereset katalog Winsock...");
            await ProcessHelper.RunProcessAsync("netsh.exe", "winsock reset");

            progress?.Invoke("Mereset tumpukan TCP/IP...");
            await ProcessHelper.RunProcessAsync("netsh.exe", "int ip reset");

            progress?.Invoke("Membersihkan cache DNS (flushdns)...");
            await ProcessHelper.RunProcessAsync("ipconfig.exe", "/flushdns");

            progress?.Invoke("Memperbarui koneksi IP (release & renew)...");
            await ProcessHelper.RunProcessAsync("ipconfig.exe", "/release");
            await ProcessHelper.RunProcessAsync("ipconfig.exe", "/renew");

            progress?.Invoke("Reset tumpukan jaringan selesai.");
            return (true, "Katalog Winsock, TCP/IP stack, dan cache DNS berhasil direset. Silakan restart komputer jika diperlukan.");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Repair Network Stack gagal: {ex.Message}");
            return (false, $"Gagal mereset jaringan: {ex.Message}");
        }
    }

    #endregion

    #region 4. Runtime Dependency Validator & Winget 1-Click Fix

    public async Task<List<RuntimeDependencyItem>> CheckRuntimeDependenciesAsync()
    {
        return await Task.Run(() =>
        {
            var list = new List<RuntimeDependencyItem>();

            // 1. Visual C++ 2015-2022 (x64)
            var vc64 = new RuntimeDependencyItem
            {
                Name = "Visual C++ 2015-2022 Redistributable (x64)",
                Category = "C++ Runtime",
                Description = "Pustaka runtime untuk aplikasi 64-bit dan game modern.",
                WingetId = "Microsoft.VCRedist.2015+.x64"
            };
            var vc64Ver = RegistryHelper.GetString(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64", "Version");
            var vc64Inst = RegistryHelper.GetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64", "Installed", 0);
            if (vc64Inst == 1 || !string.IsNullOrEmpty(vc64Ver))
            {
                vc64.IsInstalled = true;
                vc64.Version = string.IsNullOrEmpty(vc64Ver) ? "Terpasang" : $"v{vc64Ver}";
            }
            list.Add(vc64);

            // 2. Visual C++ 2015-2022 (x86)
            var vc86 = new RuntimeDependencyItem
            {
                Name = "Visual C++ 2015-2022 Redistributable (x86)",
                Category = "C++ Runtime",
                Description = "Pustaka runtime untuk kompatibilitas game & utilitas 32-bit.",
                WingetId = "Microsoft.VCRedist.2015+.x86"
            };
            var vc86Ver = RegistryHelper.GetString(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X86", "Version");
            var vc86Inst = RegistryHelper.GetDWord(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X86", "Installed", 0);
            if (vc86Inst == 1 || !string.IsNullOrEmpty(vc86Ver))
            {
                vc86.IsInstalled = true;
                vc86.Version = string.IsNullOrEmpty(vc86Ver) ? "Terpasang" : $"v{vc86Ver}";
            }
            list.Add(vc86);

            // 3. DirectX End-User Runtime
            var dx = new RuntimeDependencyItem
            {
                Name = "DirectX End-User Runtime (Legacy D3D)",
                Category = "DirectX",
                Description = "Pustaka d3dx9_43.dll dan D3DCompiler untuk game DirectX 9/10/11.",
                WingetId = "Microsoft.DirectX"
            };
            string sys32 = Environment.SystemDirectory;
            string dxDll = Path.Combine(sys32, "d3dx9_43.dll");
            string dxDll11 = Path.Combine(sys32, "d3dx11_43.dll");
            if (File.Exists(dxDll) || File.Exists(dxDll11))
            {
                dx.IsInstalled = true;
                dx.Version = "DirectX 9/11 Components OK";
            }
            list.Add(dx);

            // 4. .NET Desktop Runtime 8.0
            var net8 = new RuntimeDependencyItem
            {
                Name = ".NET Desktop Runtime 8.0 (x64)",
                Category = ".NET Runtime",
                Description = "Pustaka eksekusi aplikasi desktop modern berbasis .NET 8 WPF/WinForms.",
                WingetId = "Microsoft.DotNet.DesktopRuntime.8"
            };
            string dotnetShared = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared", "Microsoft.WindowsDesktop.App");
            if (Directory.Exists(dotnetShared))
            {
                var dirs = Directory.GetDirectories(dotnetShared, "8.*");
                if (dirs.Length > 0)
                {
                    net8.IsInstalled = true;
                    net8.Version = Path.GetFileName(dirs[0]);
                }
            }
            // Fallback check current runtime
            if (!net8.IsInstalled && Environment.Version.Major == 8)
            {
                net8.IsInstalled = true;
                net8.Version = Environment.Version.ToString();
            }
            list.Add(net8);

            return list;
        });
    }

    public async Task<(bool Success, string Message)> InstallRuntimeAsync(string wingetId, Action<string>? progress = null)
    {
        progress?.Invoke($"Mengunduh dan memasang paket {wingetId} via Winget...");
        try
        {
            var res = await ProcessHelper.RunProcessAsync("winget.exe", $"install --id {wingetId} -e --silent --accept-source-agreements --accept-package-agreements");
            if (res.ExitCode == 0)
            {
                progress?.Invoke($"Paket {wingetId} berhasil dipasang.");
                return (true, $"Paket {wingetId} berhasil dipasang ke sistem.");
            }
            else
            {
                progress?.Invoke($"Winget selesai dengan kode {res.ExitCode}.");
                return (false, $"Pemasangan selesai dengan kode {res.ExitCode}: {res.StandardOutput.Trim()}");
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Instalasi {wingetId} gagal: {ex.Message}");
            return (false, $"Gagal memasang paket: {ex.Message}");
        }
    }

    #endregion

    #region 5. Broken Shortcuts & Orphaned Registry Cleaner

    public async Task<List<BrokenShortcutItem>> ScanBrokenShortcutsAsync(Action<string>? progress = null)
    {
        progress?.Invoke("Memindai file jalan pintas (.lnk) pada Desktop dan Start Menu...");

        return await Task.Run(async () =>
        {
            var items = new List<BrokenShortcutItem>();
            string script = @"
                $shell = New-Object -ComObject WScript.Shell
                $dirs = @(
                    [Environment]::GetFolderPath('Desktop'),
                    [Environment]::GetFolderPath('CommonDesktopDirectory'),
                    [Environment]::GetFolderPath('StartMenu'),
                    [Environment]::GetFolderPath('CommonStartMenu')
                ) | Where-Object { Test-Path $_ }

                $results = @()
                foreach ($d in $dirs) {
                    $lnks = Get-ChildItem -Path $d -Filter *.lnk -Recurse -File -ErrorAction SilentlyContinue
                    foreach ($f in $lnks) {
                        try {
                            $sc = $shell.CreateShortcut($f.FullName)
                            $target = $sc.TargetPath
                            if ($target -and $target.Length -gt 3 -and $target.Substring(1,1) -eq ':') {
                                if (!(Test-Path -LiteralPath $target)) {
                                    $results += [PSCustomObject]@{
                                        FileName = $f.Name
                                        FilePath = $f.FullName
                                        BrokenTarget = $target
                                    }
                                }
                            }
                        } catch {}
                    }
                }
                $results | ConvertTo-Json -Compress
            ";

            try
            {
                var run = await ProcessHelper.RunPowerShellScriptAsync(script);
                string json = run.StandardOutput.Trim();
                if (!string.IsNullOrEmpty(json) && json.StartsWith("["))
                {
                    var parsed = JsonSerializer.Deserialize<List<BrokenShortcutItem>>(json);
                    if (parsed != null) items.AddRange(parsed);
                }
                else if (!string.IsNullOrEmpty(json) && json.StartsWith("{"))
                {
                    var single = JsonSerializer.Deserialize<BrokenShortcutItem>(json);
                    if (single != null) items.Add(single);
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Pemindaian broken shortcut gagal: {ex.Message}");
            }

            progress?.Invoke($"Ditemukan {items.Count} pintasan rusak.");
            return items;
        });
    }

    public async Task<int> CleanBrokenShortcutsAsync(IEnumerable<BrokenShortcutItem> items)
    {
        return await Task.Run(() =>
        {
            int cleaned = 0;
            foreach (var item in items)
            {
                if (!item.IsSelected) continue;
                try
                {
                    if (File.Exists(item.FilePath))
                    {
                        File.Delete(item.FilePath);
                        cleaned++;
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.Warning($"Gagal menghapus {item.FilePath}: {ex.Message}");
                }
            }
            return cleaned;
        });
    }

    public async Task<List<OrphanedRegistryItem>> ScanOrphanedRegistryKeysAsync(Action<string>? progress = null)
    {
        progress?.Invoke("Memindai entri registri uninstall yang kehilangan direktori target...");

        return await Task.Run(() =>
        {
            var list = new List<OrphanedRegistryItem>();
            string[] hives = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var hivePath in hives)
            {
                try
                {
                    using var root = Registry.LocalMachine.OpenSubKey(hivePath);
                    if (root == null) continue;

                    foreach (var subName in root.GetSubKeyNames())
                    {
                        // Lewatkan system GUIDs jika perlu, cek apakah ada DisplayName
                        using var sub = root.OpenSubKey(subName);
                        if (sub == null) continue;

                        string? displayName = sub.GetValue("DisplayName")?.ToString();
                        if (string.IsNullOrWhiteSpace(displayName)) continue;

                        // Periksa InstallLocation
                        string? installLoc = sub.GetValue("InstallLocation")?.ToString();
                        string? displayIcon = sub.GetValue("DisplayIcon")?.ToString();

                        string invalidTarget = string.Empty;

                        if (!string.IsNullOrWhiteSpace(installLoc) && installLoc.Length > 3 && installLoc.Contains(@":\"))
                        {
                            if (!Directory.Exists(installLoc))
                            {
                                invalidTarget = installLoc;
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(displayIcon) && displayIcon.Contains(@":\") && displayIcon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            string cleanPath = displayIcon.Trim('\"', '\'').Split(',')[0];
                            if (!File.Exists(cleanPath))
                            {
                                invalidTarget = cleanPath;
                            }
                        }

                        if (!string.IsNullOrEmpty(invalidTarget))
                        {
                            list.Add(new OrphanedRegistryItem
                            {
                                DisplayName = displayName,
                                KeyPath = $@"{hivePath}\{subName}",
                                InvalidPath = invalidTarget,
                                IsSelected = true
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.Warning($"Gagal memindai registry {hivePath}: {ex.Message}");
                }
            }

            progress?.Invoke($"Ditemukan {list.Count} entri uninstall orphaned.");
            return list;
        });
    }

    public async Task<int> CleanOrphanedRegistryKeysAsync(IEnumerable<OrphanedRegistryItem> items)
    {
        return await Task.Run(() =>
        {
            int deleted = 0;
            foreach (var item in items)
            {
                if (!item.IsSelected) continue;
                try
                {
                    int lastSlash = item.KeyPath.LastIndexOf('\\');
                    if (lastSlash > 0)
                    {
                        string parentPath = item.KeyPath.Substring(0, lastSlash);
                        string keyName = item.KeyPath.Substring(lastSlash + 1);

                        using var parent = Registry.LocalMachine.OpenSubKey(parentPath, true);
                        if (parent != null)
                        {
                            parent.DeleteSubKeyTree(keyName, false);
                            deleted++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.Warning($"Gagal menghapus registry orphaned {item.KeyPath}: {ex.Message}");
                }
            }
            return deleted;
        });
    }

    #endregion

    #region 6. Disk Health & CHKDSK Scheduler

    public async Task<DiskHealthReport> CheckDiskHealthAsync()
    {
        return await Task.Run(async () =>
        {
            var report = new DiskHealthReport();

            try
            {
                var drive = new DriveInfo("C");
                if (drive.IsReady)
                {
                    report.DriveLetter = drive.Name;
                    report.VolumeLabel = string.IsNullOrEmpty(drive.VolumeLabel) ? "Windows OS" : drive.VolumeLabel;
                    report.FileSystem = drive.DriveFormat;
                    report.TotalSpaceGb = Math.Round((double)drive.TotalSize / (1024 * 1024 * 1024), 1);
                    report.FreeSpaceGb = Math.Round((double)drive.TotalFreeSpace / (1024 * 1024 * 1024), 1);
                    double usedGb = report.TotalSpaceGb - report.FreeSpaceGb;
                    report.UsedPercent = Math.Round((usedGb / report.TotalSpaceGb) * 100, 1);
                }

                // Query dirty bit via fsutil
                var dirtyQuery = await ProcessHelper.RunProcessAsync("fsutil.exe", "dirty query C:");
                if (dirtyQuery.StandardOutput.Contains("is dirty", StringComparison.OrdinalIgnoreCase))
                {
                    report.IsDirty = true;
                }
                else
                {
                    report.IsDirty = false;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Gagal membaca kesehatan disk: {ex.Message}");
            }

            return report;
        });
    }

    public async Task<(bool Success, string Message)> ScheduleChkdskOnRebootAsync(string drive = "C:")
    {
        try
        {
            // Jadwalkan chkdsk C: /f /r dengan menyetel dirty bit atau via fsutil
            var res = await ProcessHelper.RunProcessAsync("fsutil.exe", $"dirty set {drive}");
            if (res.ExitCode == 0 || res.StandardOutput.Contains("Volume - C: is now marked dirty", StringComparison.OrdinalIgnoreCase))
            {
                return (true, $"Pemeriksaan integritas disk ({drive}) dijadwalkan secara otomatis pada saat komputer dinyalakan kembali (reboot).");
            }

            // Fallback via chkdsk script
            var chkRes = await ProcessHelper.RunPowerShellScriptAsync($"echo y | chkdsk.exe {drive} /f /r");
            return (true, $"Pemeriksaan disk {drive} dijadwalkan saat restart Windows berikutnya.");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Penjadwalan chkdsk gagal: {ex.Message}");
            return (false, $"Gagal menjadwalkan pemeriksaan disk: {ex.Message}");
        }
    }

    #endregion
}
