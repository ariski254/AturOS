using System.Net.NetworkInformation;
using AturOS.Helpers;
using AturOS.Models;

namespace AturOS.Services;

public class DnsOptimizerService
{
    public List<DnsPresetItem> GetPresets()
    {
        return new List<DnsPresetItem>
        {
            new DnsPresetItem
            {
                Name = "Cloudflare DNS (1.1.1.1)",
                PrimaryDns = "1.1.1.1",
                SecondaryDns = "1.0.0.1",
                Description = "DNS publik tercepat dengan fokus privasi dan tanpa logging IP."
            },
            new DnsPresetItem
            {
                Name = "Google Public DNS (8.8.8.8)",
                PrimaryDns = "8.8.8.8",
                SecondaryDns = "8.8.4.4",
                Description = "DNS global dengan keandalan tinggi dan routing terluas di dunia."
            },
            new DnsPresetItem
            {
                Name = "AdGuard DNS (Pemblokir Iklan)",
                PrimaryDns = "94.140.14.14",
                SecondaryDns = "94.140.15.15",
                Description = "DNS penyaring iklan, pelacak (tracking), dan situs berbahaya otomatis."
            },
            new DnsPresetItem
            {
                Name = "Quad9 DNS (Keamanan Siber)",
                PrimaryDns = "9.9.9.9",
                SecondaryDns = "149.112.112.112",
                Description = "DNS dengan perlindungan pemblokiran domain malware dan phishing aktif."
            }
        };
    }

    public async Task BenchmarkPresetAsync(DnsPresetItem preset)
    {
        preset.IsTesting = true;
        preset.PingMs = -1;

        await Task.Run(async () =>
        {
            try
            {
                using var ping = new Ping();
                long total = 0;
                int count = 0;

                for (int i = 0; i < 3; i++)
                {
                    var reply = await ping.SendPingAsync(preset.PrimaryDns, 1500);
                    if (reply.Status == IPStatus.Success)
                    {
                        total += reply.RoundtripTime;
                        count++;
                    }
                    await Task.Delay(50);
                }

                preset.PingMs = count > 0 ? (total / count) : -1;
            }
            catch
            {
                preset.PingMs = -1;
            }
            finally
            {
                preset.IsTesting = false;
            }
        });
    }

    public async Task<(bool Success, string Message)> ApplyDnsAsync(string primaryDns, string secondaryDns)
    {
        if (!AdministratorHelper.IsAdministrator)
        {
            return (false, "Mengubah pengaturan DNS adapter memerlukan hak Administrator.");
        }

        LoggerService.Instance.Info($"Menerapkan DNS: {primaryDns}, {secondaryDns}...");

        // Find active network interfaces via PowerShell
        var script = $@"
            $adapters = Get-NetAdapter | Where-Object {{ $_.Status -eq 'Up' }}
            foreach ($a in $adapters) {{
                Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ServerAddresses ('{primaryDns}', '{secondaryDns}')
            }}
            Clear-DnsClientCache
        ";

        var result = await ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 30000);

        if (result.Success)
        {
            LoggerService.Instance.Success($"DNS berhasil diubah ke {primaryDns} / {secondaryDns}.");
            return (true, $"DNS adapter aktif berhasil disetel ke {primaryDns} dan cache DNS telah dibersihkan.");
        }

        return (false, $"Gagal mengubah DNS: {result.StandardError}");
    }

    public async Task<(bool Success, string Message)> ResetToDhcpAsync()
    {
        if (!AdministratorHelper.IsAdministrator)
        {
            return (false, "Mengubah pengaturan DNS memerlukan hak Administrator.");
        }

        LoggerService.Instance.Info("Mengembalikan DNS ke DHCP otomatis...");

        var script = @"
            $adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' }
            foreach ($a in $adapters) {
                Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ResetServerAddresses
            }
            Clear-DnsClientCache
        ";

        var result = await ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 30000);

        if (result.Success)
        {
            LoggerService.Instance.Success("DNS berhasil dikembalikan ke DHCP otomatis.");
            return (true, "DNS berhasil dikembalikan ke otomatis (DHCP router / ISP).");
        }

        return (false, $"Gagal mereset DNS: {result.StandardError}");
    }
}
