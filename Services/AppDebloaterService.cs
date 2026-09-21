using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AturOS.Helpers;
using AturOS.Models;
using Microsoft.Win32;

namespace AturOS.Services;

public class AppDebloaterService
{
    // Paket host internal OS esensial yang tidak boleh dihapus agar shell Windows tidak hancur (sesuai Security Rules AGENTS.md)
    private static readonly HashSet<string> EssentialCoreSystemPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows.immersivecontrolpanel",          // Aplikasi Settings Windows
        "Microsoft.Windows.ShellExperienceHost",  // Shell taskbar/alt-tab
        "Microsoft.Windows.StartMenuExperienceHost", // Start menu Windows
        "Microsoft.SecHealthUI",                  // Windows Security UI
        "Microsoft.Windows.CloudExperienceHost",  // Host login Windows
        "Microsoft.Windows.OOBENetworkCaptivePortal",
        "Microsoft.Windows.OOBENetworkConnectionFlow",
        "Microsoft.AccountsControl",              // Windows Account Manager
        "Microsoft.AAD.BrokerPlugin",             // SSO Enterprise Plugin
        "Microsoft.BioEnrollment",                // Windows Hello Biometrics
        "Microsoft.CredDialogHost",               // UAC Credential Dialog
        "Microsoft.LockApp",                      // Lockscreen Windows
        "Microsoft.Windows.PinningConfirmationDialog",
        "Windows.PrintDialog"                     // Dialog Cetak Dasar
    };

    private static readonly Dictionary<string, (string FriendlyName, string Description, string Category, bool SafeToRemove, string StoreUrl)> KnownUwpApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.XboxGamingOverlay"] = ("Xbox Game Bar", "Overlay perekaman gameplay dan widget gaming Windows.", "Gaming & Hiburan", true, "ms-windows-store://pdp/?ProductId=9NZKPSTSNW4P"),
        ["Microsoft.XboxApp"] = ("Xbox App", "Aplikasi manajemen game dan langganan PC Game Pass.", "Gaming & Hiburan", true, "ms-windows-store://pdp/?ProductId=9MV0B5HZVK9Z"),
        ["Microsoft.XboxIdentityProvider"] = ("Xbox Identity Provider", "Layanan otentikasi profil Xbox Live.", "Gaming & Hiburan", true, "ms-windows-store://pdp/?ProductId=9WZDNCRD1HKW"),
        ["Microsoft.XboxSpeechToTextOverlay"] = ("Xbox Speech To Text", "Overlay transkripsi suara obrolan Xbox.", "Gaming & Hiburan", true, ""),
        ["Microsoft.XboxGameCallableUI"] = ("Xbox Game Callable UI", "Antarmuka pemanggil sosial dan undangan Xbox.", "Gaming & Hiburan", true, ""),
        ["SpotifyAB.SpotifyMusic"] = ("Spotify", "Aplikasi streaming musik Spotify.", "Gaming & Hiburan", true, "ms-windows-store://pdp/?ProductId=9NCBCSZSJRSB"),
        ["Clipchamp.Clipchamp"] = ("Clipchamp Video Editor", "Aplikasi editor video cloud dari Microsoft.", "Gaming & Hiburan", true, "ms-windows-store://pdp/?ProductId=9P1J8S7CCWWT"),
        ["Microsoft.ZuneVideo"] = ("Media Player (Film & TV)", "Pemutar video standar Film & TV Windows.", "Gaming & Hiburan", true, "ms-windows-store://pdp/?ProductId=9WZDNCRFJ3P2"),
        ["Microsoft.ZuneMusic"] = ("Windows Media Player", "Aplikasi pemutar musik dan audio bawaan Windows.", "Gaming & Hiburan", true, "ms-windows-store://pdp/?ProductId=9WZDNCRFJ364"),
        ["Microsoft.BingNews"] = ("Microsoft News", "Aplikasi ringkasan berita harian dari portal MSN.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9WZDNCRFHVFW"),
        ["Microsoft.BingWeather"] = ("Cuaca (MSN Weather)", "Aplikasi ramalan cuaca terintegrasi Windows.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9WZDNCRFJ3Q2"),
        ["Microsoft.WindowsMaps"] = ("Windows Maps", "Peta dan navigasi offline bawaan Windows.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9WZDNCRFJ32T"),
        ["Microsoft.MicrosoftOfficeHub"] = ("Microsoft 365 (Office Hub)", "Aplikasi portal promosi Microsoft Office / 365.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9WZDNCRD29V9"),
        ["Microsoft.WindowsFeedbackHub"] = ("Feedback Hub", "Aplikasi pengiriman masukan dan laporan bug ke Microsoft.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9NBLGGH4R32N"),
        ["Microsoft.YourPhone"] = ("Phone Link (Koneksi ke HP)", "Sinkronisasi notifikasi, panggilan, dan SMS smartphone ke PC.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9NMPJ99VJBWV"),
        ["Microsoft.GetHelp"] = ("Dapatkan Bantuan (Get Help)", "Aplikasi asisten pencarian solusi troubleshooting Windows.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9PKDZBMV1H3T"),
        ["Microsoft.Getstarted"] = ("Tips (Get Started)", "Panduan pengenalan fitur baru Windows.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9WZDNCRFJBD8"),
        ["Microsoft.WindowsSoundRecorder"] = ("Perekam Suara (Voice Recorder)", "Aplikasi perekam audio mikrofon sederhana.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9WZDNCRFHWKN"),
        ["Microsoft.WindowsAlarms"] = ("Jam & Alarm (Windows Clock)", "Aplikasi jam, alarm, timer, dan stopwatch.", "Aplikasi Sistem Windows", false, "ms-windows-store://pdp/?ProductId=9WZDNCRFJ3PR"),
        ["Microsoft.WindowsCalculator"] = ("Kalkulator (Windows Calculator)", "Aplikasi kalkulator standar dan ilmiah Windows.", "Aplikasi Sistem Windows", false, "ms-windows-store://pdp/?ProductId=9WZDNCRFHVN5"),
        ["Microsoft.WindowsCamera"] = ("Kamera (Windows Camera)", "Aplikasi penangkap video dan foto webcam bawaan.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9WZDNCRFJBBG"),
        ["Microsoft.Paint"] = ("Paint", "Aplikasi editor gambar dan sketsa klasik Windows.", "Aplikasi Sistem Windows", false, "ms-windows-store://pdp/?ProductId=9PCFS5B6T72H"),
        ["Microsoft.WindowsNotepad"] = ("Notepad", "Editor teks ringkas standar Windows.", "Aplikasi Sistem Windows", false, "ms-windows-store://pdp/?ProductId=9MSMLRH6LZF3"),
        ["Microsoft.ScreenSketch"] = ("Snipping Tool", "Alat tangkapan layar dan perekam layar desktop.", "Aplikasi Sistem Windows", false, "ms-windows-store://pdp/?ProductId=9MZ95KL8MR0L"),
        ["Microsoft.WindowsTerminal"] = ("Windows Terminal", "Emulator terminal modern untuk PowerShell, CMD, dan WSL.", "Aplikasi Sistem Windows", false, "ms-windows-store://pdp/?ProductId=9N0DX20HK701"),
        ["Microsoft.Todos"] = ("Microsoft To Do", "Aplikasi pengelola daftar tugas dan produktivitas harian.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9NBLGGH5R558"),
        ["Microsoft.PowerAutomateDesktop"] = ("Power Automate", "Otomatisasi alur kerja desktop RPA bawaan Microsoft.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9NFTCH6J7FHV"),
        ["Microsoft.549981C3F5F10"] = ("Cortana", "Asisten suara Cortana bawaan Windows.", "Aplikasi Sistem Windows", true, ""),
        ["MicrosoftWindows.Client.WebExperience"] = ("Windows Widgets (Web Experience)", "Panel widget desktop dan konten MSN Windows 11.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9MSSGKG348SP"),
        ["Microsoft.MicrosoftStickyNotes"] = ("Sticky Notes", "Aplikasi catatan tempel di desktop Windows.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9NBLGGH4QGHW"),
        ["Microsoft.People"] = ("Microsoft People (Kontak)", "Buku kontak dan integrasi pertemanan Windows.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9NBLGGH10PG8"),
        ["Microsoft.SkypeApp"] = ("Skype", "Aplikasi panggilan video dan pesan Skype.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9WZDNCRFJ364"),
        ["Microsoft.WindowsCommunicationsApps"] = ("Mail & Kalender (Outlook)", "Aplikasi email dan kalender bawaan Windows.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9WZDNCRFHVQM"),
        ["Microsoft.DevHome"] = ("Dev Home", "Pusat dashboard pengembang bawaan Windows 11.", "Aplikasi Sistem Windows", true, "ms-windows-store://pdp/?ProductId=9N799F12MDTK"),
        ["Microsoft.MixedReality.Portal"] = ("Mixed Reality Portal", "Portal headset VR / Mixed Reality Windows.", "Aplikasi Sistem Windows", true, "")
    };

    /// <summary>
    /// Memindai SELURUH aplikasi terinstall di Windows:
    /// 1. Aplikasi Terinstall Pengguna (Win32 & Store UWP)
    /// 2. Aplikasi Terinstall Sistem Windows (Aplikasi Bawaan, Provisioning, Edge, OneDrive, dll.)
    /// </summary>
    public async Task<List<DebloatAppItem>> ScanInstalledAppsAsync()
    {
        return await Task.Run(async () =>
        {
            LoggerService.Instance.Info("Memindai seluruh aplikasi sistem dan aplikasi terinstall pengguna...");
            var allApps = new List<DebloatAppItem>();

            // 1. Scan Win32 Desktop & System Applications from Registry
            var win32Apps = ScanWin32Apps();
            allApps.AddRange(win32Apps);
            LoggerService.Instance.Info($"Ditemukan {win32Apps.Count} aplikasi desktop Win32 & program sistem.");

            // 2. Scan Modern UWP & System Apps from PowerShell Get-AppxPackage
            var uwpApps = await ScanUwpAppsAsync();
            allApps.AddRange(uwpApps);
            LoggerService.Instance.Info($"Ditemukan {uwpApps.Count} paket aplikasi modern UWP & sistem Windows.");

            // 3. Scan Microsoft OneDrive if not captured
            ScanOneDrive(allApps);

            // 4. Deduplicate by DisplayName and sort alphabetically
            var distinctApps = allApps
                .GroupBy(a => a.DisplayName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(a => a.DisplayName)
                .ToList();

            LoggerService.Instance.Success($"Total {distinctApps.Count} aplikasi (sistem & terinstall) siap dikelola.");
            return distinctApps;
        });
    }

    private List<DebloatAppItem> ScanWin32Apps()
    {
        var list = new List<DebloatAppItem>();
        var seenDisplayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var hives = new[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall")
        };

        foreach (var (hive, path) in hives)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var displayName = subKey.GetValue("DisplayName")?.ToString()?.Trim();
                        if (string.IsNullOrWhiteSpace(displayName)) continue;

                        // Skip sub-feature components that have parent installer keys
                        var parentKeyName = subKey.GetValue("ParentKeyName")?.ToString();
                        if (!string.IsNullOrWhiteSpace(parentKeyName)) continue;

                        // Filter out raw OS hotfixes / KB security patches that are not standalone apps
                        if (displayName.StartsWith("KB", StringComparison.OrdinalIgnoreCase) ||
                            displayName.StartsWith("Security Update for", StringComparison.OrdinalIgnoreCase) ||
                            displayName.StartsWith("Update for Windows", StringComparison.OrdinalIgnoreCase) ||
                            displayName.StartsWith("Hotfix", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (seenDisplayNames.Contains(displayName)) continue;
                        seenDisplayNames.Add(displayName);

                        var displayVersion = subKey.GetValue("DisplayVersion")?.ToString()?.Trim() ?? "";
                        var publisher = subKey.GetValue("Publisher")?.ToString()?.Trim() ?? "";
                        var uninstallString = subKey.GetValue("UninstallString")?.ToString()?.Trim() ?? "";
                        var quietUninstallString = subKey.GetValue("QuietUninstallString")?.ToString()?.Trim() ?? "";
                        var installLocation = subKey.GetValue("InstallLocation")?.ToString()?.Trim() ?? "";

                        // Determine if it is a System Application
                        var isSystemComp = subKey.GetValue("SystemComponent");
                        bool isSystemComponent = (isSystemComp is int sysInt && sysInt == 1);
                        bool isMicrosoftSystem = publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) &&
                            (displayName.Contains("Edge", StringComparison.OrdinalIgnoreCase) ||
                             displayName.Contains("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                             displayName.Contains("Office", StringComparison.OrdinalIgnoreCase) ||
                             displayName.Contains("Cortana", StringComparison.OrdinalIgnoreCase) ||
                             displayName.Contains("Visual C++", StringComparison.OrdinalIgnoreCase) ||
                             displayName.Contains(".NET", StringComparison.OrdinalIgnoreCase));

                        bool isSystemApp = isSystemComponent || isMicrosoftSystem;

                        // Categorize Win32 App
                        string category;
                        if (isSystemApp)
                        {
                            category = "Aplikasi Sistem Windows";
                        }
                        else if (displayName.Contains("Game", StringComparison.OrdinalIgnoreCase) ||
                                 displayName.Contains("Steam", StringComparison.OrdinalIgnoreCase) ||
                                 displayName.Contains("Epic", StringComparison.OrdinalIgnoreCase) ||
                                 displayName.Contains("Riot", StringComparison.OrdinalIgnoreCase) ||
                                 displayName.Contains("Roblox", StringComparison.OrdinalIgnoreCase) ||
                                 displayName.Contains("BlueStacks", StringComparison.OrdinalIgnoreCase))
                        {
                            category = "Gaming & Hiburan";
                        }
                        else
                        {
                            category = "Aplikasi Desktop (Win32)";
                        }

                        list.Add(new DebloatAppItem
                        {
                            PackageName = subKeyName,
                            DisplayName = displayName,
                            Publisher = publisher,
                            Version = displayVersion,
                            Description = !string.IsNullOrWhiteSpace(publisher) ? $"{publisher} • v{displayVersion}" : $"Aplikasi desktop Win32 • v{displayVersion}",
                            Category = category,
                            AppType = AppInstallType.Win32,
                            IsSystemApp = isSystemApp,
                            UninstallString = uninstallString,
                            QuietUninstallString = quietUninstallString,
                            InstallLocation = installLocation,
                            IsSafeToRemove = !isSystemComponent,
                            IsInstalled = true
                        });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Warning($"Gagal membaca registry uninstall ({path}): {ex.Message}");
            }
        }

        return list;
    }

    private async Task<List<DebloatAppItem>> ScanUwpAppsAsync()
    {
        var list = new List<DebloatAppItem>();

        try
        {
            // Ambil seluruh paket UWP (baik aplikasi pengguna maupun aplikasi sistem yang terpasang)
            var script = @"
                Get-AppxPackage | Where-Object { 
                    -not $_.IsFramework 
                } | Select-Object Name, PackageFullName, Version, Publisher, InstallLocation, NonRemovable, SignatureKind | ConvertTo-Json -Compress
            ";

            var result = await ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 35000);
            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return list;
            }

            string json = result.StandardOutput.Trim();
            using var doc = JsonDocument.Parse(json);

            var elements = new List<JsonElement>();
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                elements.AddRange(doc.RootElement.EnumerateArray());
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                elements.Add(doc.RootElement);
            }

            foreach (var elem in elements)
            {
                string name = elem.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(name)) continue;

                // Abaikan paket host internal OS esensial (seperti Settings, Start Menu, Windows Security) agar OS tidak rusak
                if (EssentialCoreSystemPackages.Contains(name))
                {
                    continue;
                }

                // Abaikan GUID internal tanpa nama manusia
                if (Guid.TryParse(name, out _))
                {
                    continue;
                }

                string packageFullName = elem.TryGetProperty("PackageFullName", out var pfn) ? pfn.GetString() ?? name : name;
                string version = elem.TryGetProperty("Version", out var v) ? v.GetString() ?? "" : "";
                string publisher = elem.TryGetProperty("Publisher", out var pub) ? pub.GetString() ?? "" : "";
                string installLoc = elem.TryGetProperty("InstallLocation", out var loc) ? loc.GetString() ?? "" : "";
                bool nonRemovable = elem.TryGetProperty("NonRemovable", out var nr) && nr.GetBoolean();
                string sigKind = elem.TryGetProperty("SignatureKind", out var sk) ? sk.GetString() ?? "" : "";

                // Parse human publisher from CN string
                if (publisher.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                {
                    int comma = publisher.IndexOf(',');
                    publisher = comma > 3 ? publisher.Substring(3, comma - 3).Trim() : publisher.Substring(3).Trim();
                }

                string displayName = name;
                string description = "Paket aplikasi modern Windows (UWP).";
                string category = "Aplikasi Modern (UWP / Store)";
                bool safeToRemove = true;
                string storeUrl = $"ms-windows-store://search/?query={Uri.EscapeDataString(name)}";

                // Periksa apakah ini aplikasi sistem Windows
                bool isSystemApp = publisher.Contains("Microsoft Corporation", StringComparison.OrdinalIgnoreCase) ||
                                  name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                                  name.StartsWith("windows.", StringComparison.OrdinalIgnoreCase) ||
                                  sigKind.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                                  nonRemovable;

                if (KnownUwpApps.TryGetValue(name, out var known))
                {
                    displayName = known.FriendlyName;
                    description = known.Description;
                    category = known.Category;
                    safeToRemove = known.SafeToRemove;
                    storeUrl = known.StoreUrl;
                }
                else
                {
                    // Format DisplayName jika default
                    if (name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
                    {
                        displayName = name.Substring(10);
                        category = "Aplikasi Sistem Windows";
                    }
                    else if (name.Contains('.'))
                    {
                        var parts = name.Split('.');
                        displayName = parts.Length > 1 ? parts[^1] : name;
                    }

                    if (isSystemApp)
                    {
                        category = "Aplikasi Sistem Windows";
                    }
                }

                list.Add(new DebloatAppItem
                {
                    PackageName = name,
                    DisplayName = displayName,
                    Publisher = publisher,
                    Version = version,
                    Description = description,
                    Category = category,
                    AppType = AppInstallType.Uwp,
                    IsSystemApp = isSystemApp,
                    InstallLocation = installLoc,
                    IsSafeToRemove = safeToRemove,
                    StoreUrl = storeUrl,
                    IsInstalled = true
                });
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Warning($"Gagal memindai paket UWP: {ex.Message}");
        }

        return list;
    }

    private void ScanOneDrive(List<DebloatAppItem> list)
    {
        try
        {
            if (list.Any(a => a.DisplayName.Contains("OneDrive", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var sysWow64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64");
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string? setupExe = null;
            if (File.Exists(Path.Combine(sysWow64, "OneDriveSetup.exe")))
            {
                setupExe = Path.Combine(sysWow64, "OneDriveSetup.exe");
            }
            else if (File.Exists(Path.Combine(sysDir, "OneDriveSetup.exe")))
            {
                setupExe = Path.Combine(sysDir, "OneDriveSetup.exe");
            }
            else if (File.Exists(Path.Combine(localApp, @"Microsoft\OneDrive\OneDrive.exe")))
            {
                setupExe = Path.Combine(localApp, @"Microsoft\OneDrive\OneDrive.exe");
            }

            if (!string.IsNullOrEmpty(setupExe))
            {
                list.Add(new DebloatAppItem
                {
                    PackageName = "Microsoft.OneDrive",
                    DisplayName = "Microsoft OneDrive",
                    Publisher = "Microsoft Corporation",
                    Version = "Built-in",
                    Description = "Layanan penyimpanan awan Microsoft OneDrive bawaan Windows.",
                    Category = "Aplikasi Sistem Windows",
                    AppType = AppInstallType.Win32,
                    IsSystemApp = true,
                    UninstallString = $"\"{setupExe}\" /uninstall",
                    QuietUninstallString = $"\"{setupExe}\" /uninstall",
                    InstallLocation = Path.GetDirectoryName(setupExe) ?? "",
                    IsSafeToRemove = true,
                    IsInstalled = true
                });
            }
        }
        catch { }
    }

    public async Task<(bool Success, string Message)> UninstallAppAsync(DebloatAppItem app)
    {
        LoggerService.Instance.Info($"Mencopot aplikasi: {app.DisplayName} ({app.AppTypeDisplay}, {app.OriginBadgeText})...");
        app.IsProcessing = true;

        try
        {
            if (app.AppType == AppInstallType.Uwp)
            {
                // Script pencopotan UWP mendalam (User & Provisioned Package)
                var script = $@"
$pkg = Get-AppxPackage -Name '{app.PackageName}'
if ($pkg) {{
    $pkg | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
    $pkg | Remove-AppxPackage -ErrorAction SilentlyContinue
}}
$prov = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object {{ $_.DisplayName -eq '{app.PackageName}' -or $_.PackageName -like '*{app.PackageName}*' }}
if ($prov) {{
    foreach ($p in $prov) {{
        Remove-AppxProvisionedPackage -Online -PackageName $p.PackageName -ErrorAction SilentlyContinue | Out-Null
    }}
}}
$rem = Get-AppxPackage -Name '{app.PackageName}'
if ($rem) {{
    throw ""Aplikasi masih terpasang pada profil pengguna tertentu atau sistem terlindungi.""
}}
";
                var result = await ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 45000);

                if (result.Success)
                {
                    app.IsInstalled = false;
                    LoggerService.Instance.Success($"Aplikasi {app.DisplayName} berhasil didebloat/dicopot.");
                    return (true, $"Aplikasi '{app.DisplayName}' berhasil didebloat dan dicopot dari sistem.");
                }

                var error = !string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardError : result.StandardOutput;
                LoggerService.Instance.Error($"Gagal mencopot UWP {app.DisplayName}: {error}");
                return (false, $"Gagal mencopot: {error}");
            }
            else
            {
                // Win32 Uninstaller
                if (app.DisplayName.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
                {
                    // Khusus OneDrive: Hentikan proses jika sedang aktif terlebih dahulu
                    try
                    {
                        foreach (var p in Process.GetProcessesByName("OneDrive"))
                        {
                            p.Kill();
                        }
                    }
                    catch { }
                }

                string uninstCmd = !string.IsNullOrWhiteSpace(app.UninstallString)
                    ? app.UninstallString
                    : app.QuietUninstallString;

                if (string.IsNullOrWhiteSpace(uninstCmd))
                {
                    return (false, "Perintah uninstaller tidak ditemukan pada registry aplikasi ini.");
                }

                var (fileName, arguments) = ParseCommandLine(uninstCmd);

                // If msiexec, convert /I to /X for uninstallation
                if (fileName.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
                {
                    if (arguments.Contains("/I", StringComparison.OrdinalIgnoreCase))
                    {
                        arguments = arguments.Replace("/I", "/X", StringComparison.OrdinalIgnoreCase);
                    }
                }

                LoggerService.Instance.Info($"Menjalankan uninstaller: {fileName} {arguments}");

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true
                };

                var proc = Process.Start(psi);
                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                    app.IsInstalled = false;
                    LoggerService.Instance.Success($"Uninstaller '{app.DisplayName}' selesai dijalankan.");
                    return (true, $"Proses pencopotan '{app.DisplayName}' telah selesai dijalankan.");
                }

                return (false, "Gagal memulai proses uninstaller.");
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal mencopot {app.DisplayName}: {ex.Message}");
            return (false, $"Error saat uninstall: {ex.Message}");
        }
        finally
        {
            app.IsProcessing = false;
        }
    }

    private static (string FileName, string Arguments) ParseCommandLine(string commandLine)
    {
        commandLine = commandLine.Trim();
        if (string.IsNullOrEmpty(commandLine)) return ("", "");

        if (commandLine.StartsWith("\""))
        {
            int secondQuote = commandLine.IndexOf('\"', 1);
            if (secondQuote > 0)
            {
                string file = commandLine.Substring(1, secondQuote - 1);
                string args = commandLine.Substring(secondQuote + 1).Trim();
                return (file, args);
            }
        }

        int firstSpace = commandLine.IndexOf(' ');
        if (firstSpace > 0)
        {
            string file = commandLine.Substring(0, firstSpace);
            string args = commandLine.Substring(firstSpace + 1).Trim();
            return (file, args);
        }

        return (commandLine, "");
    }

    public static void OpenStoreForReinstall(DebloatAppItem app)
    {
        try
        {
            if (app.AppType == AppInstallType.Uwp)
            {
                string url = !string.IsNullOrEmpty(app.StoreUrl)
                    ? app.StoreUrl
                    : $"ms-windows-store://search/?query={Uri.EscapeDataString(app.DisplayName)}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else if (!string.IsNullOrEmpty(app.InstallLocation) && Directory.Exists(app.InstallLocation))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{app.InstallLocation}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal membuka Store/Folder: {ex.Message}");
        }
    }
}
