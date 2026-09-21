using System.Text.RegularExpressions;
using AturOS.Helpers;
using AturOS.Models;

namespace AturOS.Services;

public class PowerPlanService
{
    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string UltimateBaseGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

    public TweakItem GetPowerPlanTweakItem()
    {
        return new TweakItem
        {
            Id = "ultimate_performance",
            Name = "Mode Daya Kinerja Maksimal (Ultimate Performance)",
            Description = "Mengaktifkan profil daya tersembunyi Windows yang menghilangkan micro-latency untuk performa maksimal pada PC workstation / gaming.",
            Category = "Performa",
            RequiresElevation = false,
            RequiresExplorerRestart = false
        };
    }

    public async Task RefreshPowerPlanStatusAsync(TweakItem tweak)
    {
        try
        {
            var activeSchemeResult = await ProcessHelper.RunCommandAsync("powercfg.exe", "/getactivescheme");
            if (activeSchemeResult.Success)
            {
                var output = activeSchemeResult.StandardOutput;
                if (output.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase) ||
                    output.Contains("Kinerja Maksimal", StringComparison.OrdinalIgnoreCase))
                {
                    tweak.Status = TweakStatus.Aktif;
                    tweak.StatusMessage = "Skema Ultimate Performance sedang aktif.";
                    return;
                }

                // If not ultimate, check if it's currently something else
                var match = Regex.Match(output, @"\((.*?)\)");
                var schemeName = match.Success ? match.Groups[1].Value : "Lainnya";
                tweak.Status = TweakStatus.Nonaktif;
                tweak.StatusMessage = $"Skema aktif saat ini: {schemeName}";
            }
            else
            {
                tweak.Status = TweakStatus.TidakDiketahui;
            }
        }
        catch (Exception ex)
        {
            tweak.Status = TweakStatus.TidakDiketahui;
            tweak.StatusMessage = ex.Message;
            LoggerService.Instance.Warning($"Cek status Power Plan gagal: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> EnableUltimatePerformanceAsync(TweakItem tweak)
    {
        try
        {
            LoggerService.Instance.Info("Mencari atau mengaktifkan Ultimate Performance...");

            // 1. List power plans to see if Ultimate already exists
            var listResult = await ProcessHelper.RunCommandAsync("powercfg.exe", "/list");
            string? ultimateGuid = null;

            if (listResult.Success)
            {
                // Parse lines like: Power Scheme GUID: <guid>  (Ultimate Performance)
                var lines = listResult.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase) ||
                        line.Contains("Kinerja Maksimal", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(line, @"[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}");
                        if (match.Success)
                        {
                            ultimateGuid = match.Value;
                            break;
                        }
                    }
                }
            }

            // 2. If not found, duplicate scheme
            if (string.IsNullOrEmpty(ultimateGuid))
            {
                LoggerService.Instance.Info("Menduplikasi skema Ultimate Performance...");
                var dupResult = await ProcessHelper.RunCommandAsync("powercfg.exe", $"-duplicatescheme {UltimateBaseGuid}");
                if (dupResult.Success)
                {
                    var match = Regex.Match(dupResult.StandardOutput, @"[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}");
                    if (match.Success)
                    {
                        ultimateGuid = match.Value;
                    }
                }
            }

            // 3. Set active using found GUID
            if (!string.IsNullOrEmpty(ultimateGuid))
            {
                var setResult = await ProcessHelper.RunCommandAsync("powercfg.exe", $"/setactive {ultimateGuid}");
                if (setResult.Success)
                {
                    await RefreshPowerPlanStatusAsync(tweak);
                    LoggerService.Instance.Success($"Ultimate Performance ({ultimateGuid}) berhasil diaktifkan.");
                    return (true, "Ultimate Performance Power Plan berhasil diaktifkan.");
                }
            }

            return (false, "Gagal mengaktifkan Ultimate Performance pada sistem ini.");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Error Ultimate Performance: {ex.Message}");
            return (false, $"Error: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> RestoreBalancedPlanAsync(TweakItem tweak)
    {
        try
        {
            LoggerService.Instance.Info("Mengembalikan ke skema Balanced...");
            var setResult = await ProcessHelper.RunCommandAsync("powercfg.exe", $"/setactive {BalancedGuid}");
            if (setResult.Success)
            {
                await RefreshPowerPlanStatusAsync(tweak);
                LoggerService.Instance.Success("Skema daya seimbang (Balanced) berhasil diaktifkan.");
                return (true, "Skema daya berhasil dikembalikan ke Seimbang (Balanced).");
            }

            return (false, "Gagal mengaktifkan skema Balanced.");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal restore Balanced plan: {ex.Message}");
            return (false, $"Error: {ex.Message}");
        }
    }
}
