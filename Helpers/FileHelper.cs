using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using AturOS.Services;

namespace AturOS.Helpers;

public static class FileHelper
{
    /// <summary>
    /// Menghapus folder dan seluruh isinya secara permanen dan tuntas.
    /// Mematikan proses pengunci, mencabut proteksi read-only, dan menghapus direktori hingga bersih.
    /// </summary>
    public static bool DeleteDirectoryPermanently(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return false;

        string fullPath = Path.GetFullPath(path).TrimEnd('\\', '/');

        // Safety Guard: Perlindungan ketat direktori vital sistem operasi
        if (IsCriticalSystemDirectory(fullPath))
        {
            LoggerService.Instance.Warning($"Penghapusan folder {fullPath} dicegah demi integritas sistem.");
            return false;
        }

        try
        {
            // 1. Hentikan proses yang berjalan dari dalam direktori ini agar file tidak terkunci
            KillProcessesInDirectory(fullPath);

            // 2. Cabut atribut ReadOnly, Hidden, dan System dari seluruh berkas
            var dirInfo = new DirectoryInfo(fullPath);
            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                try
                {
                    file.Attributes = FileAttributes.Normal;
                }
                catch { }
            }

            // 3. Hapus direktori secara rekursif
            Directory.Delete(fullPath, true);
            LoggerService.Instance.Success($"Folder aplikasi berhasil dihapus permanen: {fullPath}");
            return true;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Pemberhentian normal pada {fullPath} terhambat: {ex.Message}. Mencoba pembersihan paksa via Takeown & RMDIR...");
            try
            {
                // Fallback: Take ownership & force rmdir
                Process.Start(new ProcessStartInfo
                {
                    FileName = "takeown.exe",
                    Arguments = $"/f \"{fullPath}\" /r /d y",
                    CreateNoWindow = true,
                    UseShellExecute = false
                })?.WaitForExit(3000);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "icacls.exe",
                    Arguments = $"\"{fullPath}\" /grant administrators:F /t /c",
                    CreateNoWindow = true,
                    UseShellExecute = false
                })?.WaitForExit(3000);

                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c rd /s /q \"{fullPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                proc?.WaitForExit(5000);

                bool deleted = !Directory.Exists(fullPath);
                if (deleted)
                {
                    LoggerService.Instance.Success($"Folder {fullPath} berhasil dihapus via Takeown/RMDIR.");
                }
                else
                {
                    // Jadwalkan file dan folder yang masih terkunci agar dihapus saat reboot
                    try
                    {
                        var remainingDir = new DirectoryInfo(fullPath);
                        foreach (var f in remainingDir.GetFiles("*", SearchOption.AllDirectories))
                        {
                            NativeMethods.MoveFileEx(f.FullName, null, NativeMethods.MOVEFILE_DELAY_UNTIL_REBOOT);
                        }
                        foreach (var d in remainingDir.GetDirectories("*", SearchOption.AllDirectories).OrderByDescending(x => x.FullName.Length))
                        {
                            NativeMethods.MoveFileEx(d.FullName, null, NativeMethods.MOVEFILE_DELAY_UNTIL_REBOOT);
                        }
                        NativeMethods.MoveFileEx(fullPath, null, NativeMethods.MOVEFILE_DELAY_UNTIL_REBOOT);
                        LoggerService.Instance.Warning($"Sisa berkas terkunci di {fullPath} telah dijadwalkan untuk dihapus saat reboot sistem.");
                    }
                    catch { }
                }
                return deleted;
            }
            catch (Exception fallbackEx)
            {
                LoggerService.Instance.Error($"Gagal menghapus folder {fullPath}: {fallbackEx.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Membersihkan seluruh folder residu aplikasi Win32 (Program Files, AppData Local/Roaming, ProgramData).
    /// </summary>
    public static void PurgeWin32AppResiduals(string appName, string? installLocation = null, string? publisher = null)
    {
        if (string.IsNullOrWhiteSpace(appName)) return;

        // 1. Hapus InstallLocation utama jika ada
        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            DeleteDirectoryPermanently(installLocation);
        }

        // 2. Bersihkan nama aplikasi dari karakter tidak valid untuk folder
        string cleanName = string.Join("_", appName.Split(Path.GetInvalidFileNameChars())).Trim();
        if (cleanName.Length < 3) return; // Mencegah nama terlalu pendek

        string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roamingApp = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var candidateFolders = new[]
        {
            Path.Combine(localApp, cleanName),
            Path.Combine(roamingApp, cleanName),
            Path.Combine(programData, cleanName),
            Path.Combine(progFiles, cleanName),
            Path.Combine(progFilesX86, cleanName)
        };

        foreach (var dir in candidateFolders)
        {
            if (Directory.Exists(dir))
            {
                DeleteDirectoryPermanently(dir);
            }
        }

        // Jika publisher diketahui, periksa subfolder publisher (misal: Local\Publisher\AppName)
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            string cleanPublisher = string.Join("_", publisher.Split(Path.GetInvalidFileNameChars())).Trim();
            if (cleanPublisher.Length >= 3 && !cleanPublisher.Equals("Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
            {
                var pubCandidates = new[]
                {
                    Path.Combine(localApp, cleanPublisher, cleanName),
                    Path.Combine(roamingApp, cleanPublisher, cleanName),
                    Path.Combine(programData, cleanPublisher, cleanName),
                    Path.Combine(progFiles, cleanPublisher, cleanName),
                    Path.Combine(progFilesX86, cleanPublisher, cleanName)
                };

                foreach (var dir in pubCandidates)
                {
                    if (Directory.Exists(dir))
                    {
                        DeleteDirectoryPermanently(dir);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Membersihkan seluruh folder residu paket UWP di AppData\Local\Packages dan WindowsApps.
    /// </summary>
    public static void PurgeUwpAppResiduals(string packageName, string? packageFamilyName = null, string? installLocation = null)
    {
        // 1. Hapus install location UWP di WindowsApps jika ada
        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            DeleteDirectoryPermanently(installLocation);
        }

        // 2. Hapus folder user data di AppData\Local\Packages
        try
        {
            string usersRoot = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? "C:\\Users";
            if (Directory.Exists(usersRoot))
            {
                foreach (var userDir in Directory.GetDirectories(usersRoot))
                {
                    string packagesDir = Path.Combine(userDir, @"AppData\Local\Packages");
                    if (!Directory.Exists(packagesDir)) continue;

                    string filter = !string.IsNullOrWhiteSpace(packageFamilyName)
                        ? $"*{packageFamilyName}*"
                        : $"*{packageName}*";

                    foreach (var pkgDir in Directory.GetDirectories(packagesDir, filter))
                    {
                        DeleteDirectoryPermanently(pkgDir);
                    }
                }
            }
        }
        catch { }

        // 3. Cari dan hapus di C:\Program Files\WindowsApps jika masih ada sisa
        try
        {
            string windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
            if (Directory.Exists(windowsApps))
            {
                foreach (var appDir in Directory.GetDirectories(windowsApps, $"*{packageName}*"))
                {
                    DeleteDirectoryPermanently(appDir);
                }
            }
        }
        catch { }
    }

    private static void KillProcessesInDirectory(string dirPath)
    {
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    string? exePath = proc.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath) &&
                        exePath.StartsWith(dirPath, StringComparison.OrdinalIgnoreCase))
                    {
                        proc.Kill();
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static bool IsCriticalSystemDirectory(string fullPath)
    {
        string rootDir = Path.GetPathRoot(fullPath)?.TrimEnd('\\', '/') ?? "";
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\', '/');
        string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System).TrimEnd('\\', '/');
        string userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).TrimEnd('\\', '/');
        string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).TrimEnd('\\', '/');
        string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).TrimEnd('\\', '/');
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).TrimEnd('\\', '/');

        // Direktori root absolut yang dilarang dihapus utuh
        if (string.Equals(fullPath, rootDir, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, winDir, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, sysDir, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, userDir, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, progFiles, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, progFilesX86, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, localApp, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, appData, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, Path.Combine(userDir, "Desktop"), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, Path.Combine(userDir, "Documents"), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fullPath, Path.Combine(userDir, "Pictures"), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
