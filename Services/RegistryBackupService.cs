using System.IO;
using AturOS.Helpers;

namespace AturOS.Services;

public class RegistryBackupService
{
    private static readonly string BackupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AturOS", "RegistryBackups");

    public RegistryBackupService()
    {
        if (!Directory.Exists(BackupDir))
        {
            Directory.CreateDirectory(BackupDir);
        }
    }

    public async Task<(bool Success, string FilePath)> BackupKeyAsync(string registryKeyPath, string namePrefix = "Backup")
    {
        var safePrefix = new string(namePrefix.Where(char.IsLetterOrDigit).ToArray());
        var fileName = $"{safePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.reg";
        var fullPath = Path.Combine(BackupDir, fileName);

        LoggerService.Instance.Info($"Mencadangkan registry {registryKeyPath} ke {fullPath}...");

        var result = await ProcessHelper.RunCommandAsync("reg.exe", $"export \"{registryKeyPath}\" \"{fullPath}\" /y");

        if (result.Success && File.Exists(fullPath))
        {
            LoggerService.Instance.Success($"Cadangan registry berhasil dibuat di: {fullPath}");
            return (true, fullPath);
        }

        LoggerService.Instance.Error($"Gagal mencadangkan registry: {result.StandardError}");
        return (false, result.StandardError);
    }

    public async Task<(bool Success, string Message)> RestoreBackupAsync(string filePath)
    {
        if (!File.Exists(filePath)) return (false, "File cadangan .reg tidak ditemukan.");

        LoggerService.Instance.Info($"Memulihkan cadangan registry dari {filePath}...");

        var result = await ProcessHelper.RunCommandAsync("reg.exe", $"import \"{filePath}\"");

        if (result.Success)
        {
            LoggerService.Instance.Success("Cadangan registry berhasil dipulihkan.");
            return (true, "Pengaturan registri berhasil dipulihkan dari file cadangan.");
        }

        return (false, $"Gagal memulihkan registri: {result.StandardError}");
    }

    public static string GetBackupDirectory() => BackupDir;

    public static void OpenBackupFolder()
    {
        try
        {
            if (!Directory.Exists(BackupDir))
            {
                Directory.CreateDirectory(BackupDir);
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = BackupDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal membuka folder cadangan: {ex.Message}");
        }
    }

    public static void OpenInNotepad(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal membuka berkas di Notepad: {ex.Message}");
        }
    }

    public bool DeleteBackup(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal menghapus berkas cadangan: {ex.Message}");
            return false;
        }
    }

    public List<RegistryBackupItem> GetAvailableBackupItems()
    {
        var list = new List<RegistryBackupItem>();
        try
        {
            var dir = new DirectoryInfo(BackupDir);
            if (!dir.Exists) return list;

            foreach (var file in dir.GetFiles("*.reg").OrderByDescending(f => f.CreationTime))
            {
                list.Add(new RegistryBackupItem
                {
                    FileName = file.Name,
                    FullPath = file.FullName,
                    FormattedDate = file.CreationTime.ToString("dd MMM yyyy, HH:mm"),
                    FormattedSize = FormatFileSize(file.Length)
                });
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal memuat berkas cadangan registry: {ex.Message}");
        }
        return list;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
        return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
    }
}

public class RegistryBackupItem
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string FormattedDate { get; set; } = string.Empty;
    public string FormattedSize { get; set; } = string.Empty;
}
