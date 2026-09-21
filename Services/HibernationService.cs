using System.IO;
using AturOS.Helpers;
using Microsoft.Win32;

namespace AturOS.Services;

public enum HibernationStatus
{
    Aktif,
    Nonaktif,
    TidakDiketahui
}

public class HibernationService
{
    public HibernationStatus QueryStatus()
    {
        try
        {
            // 1. Check registry HKLM\SYSTEM\CurrentControlSet\Control\Power\HibernateEnabled
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power");
            if (key != null)
            {
                var val = key.GetValue("HibernateEnabled");
                if (val is int intVal)
                {
                    return intVal == 1 ? HibernationStatus.Aktif : HibernationStatus.Nonaktif;
                }
            }

            // 2. Secondary check: hiberfil.sys on C:
            if (File.Exists(@"C:\hiberfil.sys"))
            {
                return HibernationStatus.Aktif;
            }

            return HibernationStatus.Nonaktif;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Query status hibernasi gagal: {ex.Message}");
            return HibernationStatus.TidakDiketahui;
        }
    }

    public async Task<(bool Success, string Message)> SetHibernationAsync(bool enable)
    {
        string arg = enable ? "-h on" : "-h off";
        string action = enable ? "Mengaktifkan Hibernasi" : "Menonaktifkan Hibernasi";

        LoggerService.Instance.Info($"{action} (powercfg {arg})...");

        if (!AdministratorHelper.IsAdministrator)
        {
            return (false, "Operasi ini membutuhkan hak akses Administrator.");
        }

        var result = await ProcessHelper.RunCommandAsync("powercfg.exe", arg);
        if (result.Success)
        {
            // Verify new state
            var currentStatus = QueryStatus();
            bool verified = enable ? (currentStatus == HibernationStatus.Aktif) : (currentStatus == HibernationStatus.Nonaktif);

            if (verified)
            {
                LoggerService.Instance.Success($"{action} berhasil dan terverifikasi di sistem.");
                return (true, $"{action} berhasil.");
            }
            else
            {
                LoggerService.Instance.Warning($"{action} dijalankan tetapi status sistem belum berubah.");
                return (true, $"{action} selesai (perubahan mungkin perlu waktu).");
            }
        }

        var error = !string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardError : result.StandardOutput;
        LoggerService.Instance.Error($"{action} gagal: {error}");
        return (false, $"Gagal mengubah status hibernasi: {error}");
    }
}
