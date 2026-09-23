using System.Diagnostics;
using AturOS.Helpers;
using AturOS.Models;

namespace AturOS.Services;

public class RestorePointService
{
    public async Task<(bool Success, string Message)> CreateRestorePointAsync(string description)
    {
        if (!AdministratorHelper.IsAdministrator)
        {
            return (false, "Membuat System Restore Point membutuhkan hak akses Administrator.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            description = "AturOS-Backup";
        }

        // Clean description to avoid any injection
        description = new string(description.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').ToArray());

        LoggerService.Instance.Info($"Mempersiapkan pembuatan Restore Point: '{description}'...");

        // Ensure SystemRestore frequency restriction allows creation (default 1440 min = 24h limit in Win10/11)
        // Checkpoint-Computer -Description ... -RestorePointType "MODIFY_SETTINGS"
        var script = $@"
            try {{
                # 0. Allow frequent restore point creation (bypass 1440 minute limitation in Windows 10/11)
                Set-ItemProperty -Path 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name 'SystemRestorePointCreationFrequency' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue

                # 1. Ensure System Protection is enabled on SystemDrive
                Enable-ComputerRestore -Drive ""$($env:SystemDrive)\"" -ErrorAction SilentlyContinue

                # 2. Ensure VSS and Software Shadow Copy Provider services are running
                Set-Service -Name 'vss' -StartupType Manual -ErrorAction SilentlyContinue
                Start-Service -Name 'vss' -ErrorAction SilentlyContinue
                Set-Service -Name 'swprv' -StartupType Manual -ErrorAction SilentlyContinue
                Start-Service -Name 'swprv' -ErrorAction SilentlyContinue

                # 3. Create restore point
                Checkpoint-Computer -Description '{description}' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop
                Write-Output 'BERHASIL'
            }} catch {{
                Write-Error $_.Exception.Message
                exit 1
            }}
        ";

        var result = await ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 120000);

        if (result.Success && result.StandardOutput.Contains("BERHASIL"))
        {
            LoggerService.Instance.Success($"Restore Point '{description}' berhasil dibuat.");
            return (true, $"System Restore Point '{description}' berhasil dibuat.");
        }

        var error = !string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardError : result.StandardOutput;
        LoggerService.Instance.Error($"Pembuatan Restore Point gagal: {error}");

        if (error.Contains("0x80042306") || error.Contains("disabled") || error.Contains("tidak aktif", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Perlindungan Sistem (System Restore) saat ini dinonaktifkan pada drive Windows. Silakan aktifkan terlebih dahulu di Properti Sistem.");
        }

        return (false, $"Gagal membuat Restore Point: {error}");
    }

    public async Task<List<RestorePointItem>> GetRestorePointsAsync()
    {
        var list = new List<RestorePointItem>();

        var script = @"
            $points = Get-ComputerRestorePoint -ErrorAction SilentlyContinue | Select-Object SequenceNumber, Description, CreationTime, EventType
            if ($points) {
                $points | ConvertTo-Csv -NoTypeInformation
            }
        ";

        var result = await ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 30000);
        if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            try
            {
                var lines = result.StandardOutput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var fields = ProcessHelper.ParseCsvLine(line);
                    if (fields.Count < 4) continue;
                    if (fields[0].Equals("SequenceNumber", StringComparison.OrdinalIgnoreCase)) continue;

                    if (!int.TryParse(fields[0], out int seqNum)) continue;

                    list.Add(new RestorePointItem
                    {
                        SequenceNumber = seqNum,
                        Description = fields[1],
                        CreationTime = fields[2],
                        EventType = fields[3]
                    });
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Warning($"Gagal mem-parse restore points: {ex.Message}");
            }
        }

        return list.OrderByDescending(r => r.SequenceNumber).ToList();
    }

    public static void OpenSystemProtectionSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "SystemPropertiesProtection.exe",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal membuka SystemPropertiesProtection: {ex.Message}");
        }
    }

    public static void OpenRestoreWizard()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "rstrui.exe",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal membuka System Restore Wizard (rstrui.exe): {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> RestoreToSequenceAsync(int sequenceNumber)
    {
        if (!AdministratorHelper.IsAdministrator)
        {
            return (false, "Memulihkan titik pemulihan sistem memerlukan hak akses Administrator.");
        }

        var script = $@"
            try {{
                Restore-Computer -RestorePoint {sequenceNumber} -Confirm:$false -ErrorAction Stop
                Write-Output 'BERHASIL'
            }} catch {{
                Write-Error $_.Exception.Message
                exit 1
            }}
        ";

        var result = await ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 120000);
        if (result.Success && result.StandardOutput.Contains("BERHASIL"))
        {
            return (true, $"Proses pemulihan ke Titik #{sequenceNumber} telah dimulai. Komputer akan dimulai ulang secara otomatis oleh Windows.");
        }

        return (false, $"Gagal memulihkan ke titik pemulihan: {result.StandardError}");
    }
}
