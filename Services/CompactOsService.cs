using AturOS.Helpers;

namespace AturOS.Services;

public enum CompactOsStatus
{
    Aktif,
    Nonaktif,
    TidakDiketahui
}

public class CompactOsService
{
    public async Task<CompactOsStatus> QueryStatusAsync()
    {
        try
        {
            var result = await ProcessHelper.RunCommandAsync("compact.exe", "/compactos:query");
            if (result.Success || !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                var output = result.StandardOutput.ToLowerInvariant();
                if (output.Contains("is in the compact state") || output.Contains("dalam status compact") || output.Contains("always"))
                {
                    return CompactOsStatus.Aktif;
                }
                if (output.Contains("is not in the compact state") || output.Contains("tidak dalam status compact") || output.Contains("never"))
                {
                    return CompactOsStatus.Nonaktif;
                }
            }

            LoggerService.Instance.Warning($"CompactOS query output tidak dikenal: {result.StandardOutput}");
            return CompactOsStatus.TidakDiketahui;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal query CompactOS: {ex.Message}");
            return CompactOsStatus.TidakDiketahui;
        }
    }

    public async Task<(bool Success, string Message)> SetCompactOsAsync(bool enable)
    {
        string arg = enable ? "/compactos:always" : "/compactos:never";
        string actionName = enable ? "Aktifkan CompactOS" : "Nonaktifkan CompactOS";

        LoggerService.Instance.Info($"Menjalankan {actionName} (compact.exe {arg})...");

        // CompactOS requires administrative privileges
        if (!AdministratorHelper.IsAdministrator)
        {
            return (false, "Operasi ini membutuhkan hak akses Administrator.");
        }

        var result = await ProcessHelper.RunCommandAsync("compact.exe", arg, timeoutMs: 300000); // 5 min timeout as compaction can take time

        if (result.Success)
        {
            LoggerService.Instance.Success($"{actionName} berhasil diselesaikan.");
            return (true, $"{actionName} berhasil.");
        }

        var errorMsg = !string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardError : result.StandardOutput;
        LoggerService.Instance.Error($"{actionName} gagal (Exit code {result.ExitCode}): {errorMsg}");
        return (false, $"Gagal ({result.ExitCode}): {errorMsg}");
    }
}
