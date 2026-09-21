using System.IO;
using AturOS.Models;

namespace AturOS.Services;

public class CleanResult
{
    public long BytesFreed { get; set; }
    public int FilesDeleted { get; set; }
    public int FilesSkipped { get; set; }
    public string FormattedFreed => BytesFreed >= 1024L * 1024 * 1024
        ? $"{(double)BytesFreed / (1024 * 1024 * 1024):F2} GB"
        : $"{(double)BytesFreed / (1024 * 1024):F2} MB";
}

public class StorageCleanerService
{
    public List<CleanableItem> GetDefaultTargets()
    {
        var list = new List<CleanableItem>();

        // 1. User Temp
        var userTemp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        list.Add(new CleanableItem
        {
            Id = "user_temp",
            DisplayName = "File Sementara Pengguna (%TEMP%)",
            Path = userTemp,
            Description = "File cache dan data sementara yang dibuat oleh aplikasi pengguna.",
            IsSelected = true
        });

        // 2. Windows Temp
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var winTemp = Path.Combine(winDir, "Temp");
        list.Add(new CleanableItem
        {
            Id = "win_temp",
            DisplayName = "File Sementara Sistem (Windows\\Temp)",
            Path = winTemp,
            Description = "File sementara sistem Windows dan installer service.",
            IsSelected = true
        });

        // 3. SoftwareDistribution\Download
        var updateCache = Path.Combine(winDir, "SoftwareDistribution", "Download");
        list.Add(new CleanableItem
        {
            Id = "update_cache",
            DisplayName = "Cache Windows Update (SoftwareDistribution)",
            Path = updateCache,
            Description = "File instalasi pembaruan Windows yang sudah selesai diunduh.",
            IsSelected = true
        });

        // 4. Windows Prefetch
        var prefetch = Path.Combine(winDir, "Prefetch");
        list.Add(new CleanableItem
        {
            Id = "prefetch",
            DisplayName = "Windows Prefetch Cache",
            Path = prefetch,
            Description = "Cache pelacak peluncuran aplikasi lama untuk mempercepat pemuatan awal.",
            IsSelected = true
        });

        // 5. Crash Dumps
        var crashDumps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps");
        list.Add(new CleanableItem
        {
            Id = "crash_dumps",
            DisplayName = "Laporan Kerusakan & Crash Dumps",
            Path = crashDumps,
            Description = "File memori dump (.dmp) aplikasi yang pernah mengalami crash/error.",
            IsSelected = true
        });

        // 6. Delivery Optimization
        var deliveryOpt = Path.Combine(winDir, "SoftwareDistribution", "DeliveryOptimization");
        list.Add(new CleanableItem
        {
            Id = "delivery_opt",
            DisplayName = "Cache Pengoptimalan Pengiriman (Delivery Optimization)",
            Path = deliveryOpt,
            Description = "File cache peer-to-peer unduhan pembaruan Windows.",
            IsSelected = true
        });

        // 7. Thumbnail Cache
        var thumbCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer");
        list.Add(new CleanableItem
        {
            Id = "thumb_cache",
            DisplayName = "Cache Gambar Mini (Explorer Thumbnails)",
            Path = thumbCache,
            Description = "Cache pratinjau thumbnail file dan folder Windows Explorer.",
            IsSelected = false
        });

        // 8. Windows System Logs
        var winLogs = Path.Combine(winDir, "Logs");
        list.Add(new CleanableItem
        {
            Id = "win_logs",
            DisplayName = "Log Riwayat Sistem & Instalasi",
            Path = winLogs,
            Description = "Catatan riwayat instalasi dan diagnostik sistem Windows.",
            IsSelected = true
        });

        return list;
    }

    // ==========================================
    // TWEAK SISTEM PENYIMPANAN
    // ==========================================
    public bool GetStorageSenseStatus()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy");
            var val = key?.GetValue("01");
            return val is int i && i == 1;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetStorageSenseAsync(bool enable)
    {
        return await Task.Run(() =>
        {
            bool ok = Helpers.RegistryHelper.SetDWord(Microsoft.Win32.RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01", enable ? 1 : 0, backup: true);
            return (ok, ok ? (enable ? "Windows Storage Sense (Penyimpanan Pintar) berhasil diaktifkan." : "Windows Storage Sense dinonaktifkan.") : "Gagal mengubah status Storage Sense.");
        });
    }

    public bool GetNtfsLastAccessUpdateStatus()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\FileSystem");
            var val = key?.GetValue("NtfsDisableLastAccessUpdate");
            return val is int i && i == 1;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetNtfsLastAccessUpdateAsync(bool disableUpdate)
    {
        return await Task.Run(async () =>
        {
            if (!Helpers.AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");

            bool ok = Helpers.RegistryHelper.SetDWord(Microsoft.Win32.RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsDisableLastAccessUpdate", disableUpdate ? 1 : 0, backup: true);
            await Helpers.ProcessHelper.RunCommandAsync("fsutil.exe", $"behavior set disablelastaccess {(disableUpdate ? 1 : 0)}");

            return (ok, disableUpdate ? "Pencatatan waktu akses terakhir NTFS dinonaktifkan (menghemat I/O dan memperpanjang umur SSD)." : "Pencatatan waktu akses terakhir NTFS dikembalikan ke default.");
        });
    }

    public async Task<bool?> GetReservedStorageStatusAsync()
    {
        var res = await Helpers.ProcessHelper.RunCommandAsync("dism.exe", "/Online /Get-ReservedStorageState");
        if (res.Success)
        {
            if (res.StandardOutput.Contains("Enabled", StringComparison.OrdinalIgnoreCase))
                return true;
            if (res.StandardOutput.Contains("Disabled", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return null;
    }

    public async Task<(bool Success, string Message)> SetReservedStorageAsync(bool enable)
    {
        if (!Helpers.AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");

        string state = enable ? "Enabled" : "Disabled";
        var res = await Helpers.ProcessHelper.RunCommandAsync("dism.exe", $"/Online /Set-ReservedStorageState /State:{state}", timeoutMs: 60000);
        if (res.Success)
        {
            return (true, enable ? "Penyimpanan Cadangan Windows (Reserved Storage ~7 GB) diaktifkan." : "Penyimpanan Cadangan Windows (Reserved Storage) dinonaktifkan. Anda menghemat hingga ~7 GB ruang drive C:.");
        }
        return (false, $"Gagal mengubah status Reserved Storage ({res.ExitCode}): {res.StandardError}");
    }

    public async Task<(bool Success, string Message)> RunDismComponentCleanupAsync(Action<string>? onProgress = null)
    {
        LoggerService.Instance.Info("Menjalankan pembersihan komponen WinSxS via DISM...");
        onProgress?.Invoke("Menjalankan Dism.exe /online /Cleanup-Image /StartComponentCleanup (bisa memakan waktu beberapa menit)...");

        if (!Helpers.AdministratorHelper.IsAdministrator)
        {
            return (false, "Pembersihan WinSxS memerlukan hak akses Administrator.");
        }

        var result = await Helpers.ProcessHelper.RunCommandAsync(
            "dism.exe",
            "/online /Cleanup-Image /StartComponentCleanup",
            timeoutMs: 300000); // 5 minutes timeout

        if (result.Success)
        {
            LoggerService.Instance.Success("Pembersihan komponen WinSxS berhasil diselesaikan.");
            return (true, "Pembersihan folder WinSxS berhasil membebaskan ruang paket pembaruan lama.");
        }

        var error = !string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardError : result.StandardOutput;
        LoggerService.Instance.Error($"DISM Component Cleanup gagal: {error}");
        return (false, $"DISM gagal ({result.ExitCode}): {error}");
    }

    public async Task ScanTargetAsync(CleanableItem item, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            item.Status = "Memindai...";
            item.SizeBytes = 0;
            item.FileCount = 0;

            if (!Directory.Exists(item.Path))
            {
                item.Status = "Direktori tidak ditemukan";
                return;
            }

            try
            {
                var dirInfo = new DirectoryInfo(item.Path);
                int count = 0;
                long totalBytes = 0;

                EnumerateDirectorySafe(dirInfo, ref count, ref totalBytes, cancellationToken);

                item.FileCount = count;
                item.SizeBytes = totalBytes;
                item.Status = count > 0 ? "Siap dibersihkan" : "Bersih";
                LoggerService.Instance.Info($"Scan {item.DisplayName}: {count} file ({item.FormattedSize})");
            }
            catch (Exception ex)
            {
                item.Status = $"Error: {ex.Message}";
                LoggerService.Instance.Warning($"Scan {item.DisplayName} gagal: {ex.Message}");
            }
        }, cancellationToken);
    }

    private void EnumerateDirectorySafe(DirectoryInfo dir, ref int count, ref long totalBytes, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        try
        {
            foreach (var file in dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    totalBytes += file.Length;
                    count++;
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (var subDir in dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                if (ct.IsCancellationRequested) return;
                EnumerateDirectorySafe(subDir, ref count, ref totalBytes, ct);
            }
        }
        catch { }
    }

    public async Task<CleanResult> CleanTargetsAsync(IEnumerable<CleanableItem> items, Action<string>? onProgress = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var result = new CleanResult();

            foreach (var item in items.Where(i => i.IsSelected))
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (!Directory.Exists(item.Path)) continue;

                onProgress?.Invoke($"Membersihkan: {item.DisplayName}...");
                LoggerService.Instance.Info($"Mulai pembersihan: {item.DisplayName}");

                var dir = new DirectoryInfo(item.Path);
                DeleteDirectoryContentsSafe(dir, result, onProgress, cancellationToken);

                // Re-scan after clean to update display
                int count = 0;
                long totalBytes = 0;
                EnumerateDirectorySafe(dir, ref count, ref totalBytes, CancellationToken.None);
                item.FileCount = count;
                item.SizeBytes = totalBytes;
                item.Status = count == 0 ? "Bersih" : $"{count} file tersisa (in-use)";
            }

            LoggerService.Instance.Success($"Pembersihan selesai: {result.FilesDeleted} file dihapus ({result.FormattedFreed}), {result.FilesSkipped} file dilewati.");
            return result;
        }, cancellationToken);
    }

    private void DeleteDirectoryContentsSafe(DirectoryInfo dir, CleanResult result, Action<string>? onProgress, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        // Delete files
        try
        {
            foreach (var file in dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    long length = 0;
                    try { length = file.Length; } catch { }

                    file.Attributes = FileAttributes.Normal;
                    file.Delete();

                    result.BytesFreed += length;
                    result.FilesDeleted++;
                }
                catch (UnauthorizedAccessException)
                {
                    result.FilesSkipped++;
                }
                catch (IOException)
                {
                    // File in use by another process
                    result.FilesSkipped++;
                }
                catch (Exception)
                {
                    result.FilesSkipped++;
                }
            }
        }
        catch { }

        // Recurse subdirectories
        try
        {
            foreach (var subDir in dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                if (ct.IsCancellationRequested) return;

                DeleteDirectoryContentsSafe(subDir, result, onProgress, ct);

                // Try deleting empty directory
                try
                {
                    if (!subDir.EnumerateFileSystemInfos().Any())
                    {
                        subDir.Delete();
                    }
                }
                catch { }
            }
        }
        catch { }
    }
}
