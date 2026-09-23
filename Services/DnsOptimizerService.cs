using System.IO;
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

        try
        {
            long measuredLatency = await Task.Run(async () =>
            {
                var testDomains = new[] { "google.com", "cloudflare.com", "microsoft.com" };
                long totalLatency = 0;
                int successfulQueries = 0;

                foreach (var domain in testDomains)
                {
                    try
                    {
                        var latency = await QueryDnsResolutionLatencyAsync(preset.PrimaryDns, domain, timeoutMs: 1800);
                        if (latency >= 0)
                        {
                            totalLatency += latency;
                            successfulQueries++;
                        }
                    }
                    catch
                    {
                        // Domain query error or timeout, continue to next domain
                    }
                }

                return successfulQueries > 0 ? (totalLatency / successfulQueries) : -1;
            });

            preset.PingMs = measuredLatency;
            if (measuredLatency < 0)
            {
                LoggerService.Instance.Warning($"Pengujian latensi DNS {preset.Name} ({preset.PrimaryDns}): tidak ada respons dari server dalam batas waktu.");
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Pengujian kueri DNS {preset.Name} ({preset.PrimaryDns}) gagal: {ex.Message}");
            preset.PingMs = -1;
        }
        finally
        {
            preset.IsTesting = false;
        }
    }

    /// <summary>
    /// Melakukan kueri DNS mentah langsung ke server DNS target via UDP port 53 (FITUR.md Bab 30).
    /// Mengukur waktu bolak-balik resolusi domain aktual tanpa menggunakan ping ICMP.
    /// </summary>
    private static async Task<long> QueryDnsResolutionLatencyAsync(string dnsServerIp, string domainName, int timeoutMs = 2000)
    {
        if (!System.Net.IPAddress.TryParse(dnsServerIp, out var serverIp))
        {
            return -1;
        }

        using var udpClient = new System.Net.Sockets.UdpClient();
        udpClient.Client.ReceiveTimeout = timeoutMs;
        udpClient.Client.SendTimeout = timeoutMs;

        var queryPacket = BuildDnsQueryPacket(domainName);
        var endpoint = new System.Net.IPEndPoint(serverIp, 53);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await udpClient.SendAsync(queryPacket, queryPacket.Length, endpoint);

            using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
            var receiveTask = udpClient.ReceiveAsync();

            var completedTask = await Task.WhenAny(receiveTask, Task.Delay(timeoutMs, cts.Token));
            if (completedTask == receiveTask)
            {
                stopwatch.Stop();
                var result = await receiveTask;
                // Verifikasi ada response payload (DNS header minimal 12 byte)
                if (result.Buffer.Length >= 12)
                {
                    return Math.Max(1, stopwatch.ElapsedMilliseconds);
                }
            }
        }
        catch
        {
            return -1;
        }

        return -1;
    }

    private static byte[] BuildDnsQueryPacket(string domainName)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Header DNS (12 bytes)
        ushort id = (ushort)Random.Shared.Next(1, 65535);
        writer.Write(System.Net.IPAddress.HostToNetworkOrder((short)id));
        writer.Write(new byte[] { 0x01, 0x00 }); // Flags: Standard query, Recursion Desired
        writer.Write(System.Net.IPAddress.HostToNetworkOrder((short)1)); // QDCOUNT = 1
        writer.Write(System.Net.IPAddress.HostToNetworkOrder((short)0)); // ANCOUNT = 0
        writer.Write(System.Net.IPAddress.HostToNetworkOrder((short)0)); // NSCOUNT = 0
        writer.Write(System.Net.IPAddress.HostToNetworkOrder((short)0)); // ARCOUNT = 0

        // Question: QNAME
        foreach (var label in domainName.Split('.'))
        {
            byte len = (byte)label.Length;
            writer.Write(len);
            writer.Write(System.Text.Encoding.ASCII.GetBytes(label));
        }
        writer.Write((byte)0); // End of QNAME

        // QTYPE = 1 (A Record)
        writer.Write(System.Net.IPAddress.HostToNetworkOrder((short)1));
        // QCLASS = 1 (IN - Internet)
        writer.Write(System.Net.IPAddress.HostToNetworkOrder((short)1));

        return ms.ToArray();
    }

    public async Task<(bool Success, string Message)> ApplyDnsAsync(string primaryDns, string secondaryDns)
    {
        if (!AdministratorHelper.IsAdministrator)
        {
            return (false, "Mengubah pengaturan DNS adapter memerlukan hak Administrator.");
        }

        try
        {
            LoggerService.Instance.Info($"Menerapkan DNS: {primaryDns}, {secondaryDns}...");

            // Find active network interfaces via PowerShell
            var script = $@"
                $adapters = Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object {{ $_.Status -eq 'Up' }}
                if ($adapters) {{
                    foreach ($a in $adapters) {{
                        Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ServerAddresses ('{primaryDns}', '{secondaryDns}') -ErrorAction SilentlyContinue
                    }}
                    Clear-DnsClientCache -ErrorAction SilentlyContinue
                }}
            ";

            var result = await ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 30000);

            if (result.Success)
            {
                LoggerService.Instance.Success($"DNS berhasil diubah ke {primaryDns} / {secondaryDns}.");
                return (true, $"DNS adapter aktif berhasil disetel ke {primaryDns} dan cache DNS telah dibersihkan.");
            }

            return (false, $"Gagal mengubah DNS: {result.StandardError}");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Kesalahan saat menerapkan DNS: {ex.Message}");
            return (false, $"Terjadi kesalahan saat menerapkan DNS: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ResetToDhcpAsync()
    {
        if (!AdministratorHelper.IsAdministrator)
        {
            return (false, "Mengubah pengaturan DNS memerlukan hak Administrator.");
        }

        try
        {
            LoggerService.Instance.Info("Mengembalikan DNS ke DHCP otomatis...");

            var script = @"
                $adapters = Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'Up' }
                if ($adapters) {
                    foreach ($a in $adapters) {
                        Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ResetServerAddresses -ErrorAction SilentlyContinue
                    }
                    Clear-DnsClientCache -ErrorAction SilentlyContinue
                }
            ";

            var result = await ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 30000);

            if (result.Success)
            {
                LoggerService.Instance.Success("DNS berhasil dikembalikan ke DHCP otomatis.");
                return (true, "DNS berhasil dikembalikan ke otomatis (DHCP router / ISP).");
            }

            return (false, $"Gagal mereset DNS: {result.StandardError}");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Kesalahan saat mereset DNS: {ex.Message}");
            return (false, $"Terjadi kesalahan saat mereset DNS: {ex.Message}");
        }
    }
}
