using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

                        // Categorize Win32 App & Classify
                        var classification = ClassifyApp(displayName, publisher, subKeyName, isSystemApp, !isSystemComponent);
                        string category;
                        if (classification == AppClassification.OemBloat)
                        {
                            category = "OEM Bloatware";
                        }
                        else if (isSystemApp)
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
                            Classification = classification,
                            IsSystemApp = isSystemApp,
                            UninstallString = uninstallString,
                            QuietUninstallString = quietUninstallString,
                            InstallLocation = installLocation,
                            IsSafeToRemove = !isSystemComponent && classification != AppClassification.Essential && classification != AppClassification.Security && classification != AppClassification.Driver,
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
            // Ambil seluruh paket UWP yang terpasang menggunakan CSV (100% andal, tanpa ketergantungan JSON / Type mismatch)
            var script = @"
                Get-AppxPackage | Where-Object { 
                    -not $_.IsFramework
                } | Select-Object Name, PackageFullName, Version, Publisher, InstallLocation, SignatureKind, NonRemovable | ConvertTo-Csv -NoTypeInformation
            ";

            var result = await ProcessHelper.RunPowerShellScriptAsync(script, timeoutMs: 35000);
            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return list;
            }

            var lines = result.StandardOutput.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var fields = ProcessHelper.ParseCsvLine(line);
                if (fields.Count < 6) continue;
                if (fields[0].Equals("Name", StringComparison.OrdinalIgnoreCase)) continue; // skip header CSV

                string name = fields[0].Trim();
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

                string packageFullName = string.IsNullOrWhiteSpace(fields[1]) ? name : fields[1].Trim();
                string version = fields[2].Trim();
                string publisher = fields[3].Trim();
                string installLoc = fields[4].Trim();
                string sigKind = fields[5].Trim();
                bool isNonRemovable = fields.Count > 6 && bool.TryParse(fields[6].Trim(), out var nr) && nr;

                // Abaikan infrastruktur shell internal Windows di SystemApps (selalu diproteksi kernel Windows dan tidak dapat dicopot)
                if (installLoc.IndexOf(@"\Windows\SystemApps\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                // Abaikan jika berjenis System dan ditandai NonRemovable oleh Windows
                if (isNonRemovable && sigKind.Equals("System", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Parse human publisher from CN string
                if (publisher.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                {
                    int comma = publisher.IndexOf(',');
                    publisher = comma > 3 ? publisher.Substring(3, comma - 3).Trim() : publisher.Substring(3).Trim();
                }

                string displayName = name;
                string description = isNonRemovable 
                    ? "Paket aplikasi modern Windows (terproteksi sistem)."
                    : "Paket aplikasi modern Windows (UWP).";
                string category = "Aplikasi Modern (UWP / Store)";
                bool safeToRemove = true;
                string storeUrl = $"ms-windows-store://search/?query={Uri.EscapeDataString(name)}";

                // Periksa apakah ini aplikasi sistem Windows
                bool isSystemApp = publisher.Contains("Microsoft Corporation", StringComparison.OrdinalIgnoreCase) ||
                                  name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                                  name.StartsWith("windows.", StringComparison.OrdinalIgnoreCase) ||
                                  sigKind.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                                  isNonRemovable;

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

                var uwpClassification = ClassifyApp(displayName, publisher, name, isSystemApp, safeToRemove);
                if (uwpClassification == AppClassification.OemBloat)
                {
                    category = "OEM Bloatware";
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
                    Classification = uwpClassification,
                    IsSystemApp = isSystemApp,
                    IsNonRemovable = isNonRemovable,
                    InstallLocation = installLoc,
                    IsSafeToRemove = safeToRemove && uwpClassification != AppClassification.Essential && uwpClassification != AppClassification.Security,
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

            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var sysWow64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64");

            // 1. Periksa keberadaan file executable aktif OneDrive
            string? activeExe = null;
            if (File.Exists(Path.Combine(localApp, @"Microsoft\OneDrive\OneDrive.exe")))
            {
                activeExe = Path.Combine(localApp, @"Microsoft\OneDrive\OneDrive.exe");
            }
            else if (File.Exists(Path.Combine(progFiles, @"Microsoft OneDrive\OneDrive.exe")))
            {
                activeExe = Path.Combine(progFiles, @"Microsoft OneDrive\OneDrive.exe");
            }
            else if (File.Exists(Path.Combine(progFilesX86, @"Microsoft OneDrive\OneDrive.exe")))
            {
                activeExe = Path.Combine(progFilesX86, @"Microsoft OneDrive\OneDrive.exe");
            }

            // 2. Periksa apakah ada registrasi Registry Uninstall aktif untuk OneDrive
            bool hasRegistryEntry = false;
            string? registryUninstallCmd = null;
            try
            {
                using var uKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\OneDriveSetup.exe");
                if (uKey != null)
                {
                    hasRegistryEntry = true;
                    registryUninstallCmd = uKey.GetValue("UninstallString")?.ToString();
                }
            }
            catch { }

            if (!hasRegistryEntry)
            {
                try
                {
                    using var mKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Microsoft OneDrive");
                    if (mKey != null)
                    {
                        hasRegistryEntry = true;
                        registryUninstallCmd = mKey.GetValue("UninstallString")?.ToString();
                    }
                }
                catch { }
            }

            // PENTING: Jika file eksekusi aktif tidak ada DAN tidak ada entri registry uninstall,
            // maka OneDrive SUDAH TERHAPUS/TIDAK TERPASANG.
            // JANGAN PERNAH menganggap terinstall hanya karena System32\OneDriveSetup.exe atau SysWOW64\OneDriveSetup.exe ada,
            // karena file-file tersebut adalah berkas cetakan setup bawaan OS Windows yang tidak pernah dihapus Microsoft.
            if (activeExe == null && !hasRegistryEntry)
            {
                return;
            }

            // Tentukan perintah uninstaller yang valid
            string uninstCmd = "";
            if (!string.IsNullOrWhiteSpace(registryUninstallCmd))
            {
                uninstCmd = registryUninstallCmd;
            }
            else if (File.Exists(Path.Combine(localApp, @"Microsoft\OneDrive\OneDriveSetup.exe")))
            {
                uninstCmd = $"\"{Path.Combine(localApp, @"Microsoft\OneDrive\OneDriveSetup.exe")}\" /uninstall";
            }
            else if (File.Exists(Path.Combine(sysWow64, "OneDriveSetup.exe")))
            {
                uninstCmd = $"\"{Path.Combine(sysWow64, "OneDriveSetup.exe")}\" /uninstall";
            }
            else if (File.Exists(Path.Combine(sysDir, "OneDriveSetup.exe")))
            {
                uninstCmd = $"\"{Path.Combine(sysDir, "OneDriveSetup.exe")}\" /uninstall";
            }
            else if (!string.IsNullOrEmpty(activeExe))
            {
                uninstCmd = $"\"{activeExe}\" /uninstall";
            }

            list.Add(new DebloatAppItem
            {
                PackageName = "Microsoft.OneDrive",
                DisplayName = "Microsoft OneDrive",
                Publisher = "Microsoft Corporation",
                Version = "Terpasang",
                Description = "Layanan penyimpanan awan Microsoft OneDrive bawaan Windows.",
                Category = "Aplikasi Sistem Windows",
                AppType = AppInstallType.Win32,
                Classification = AppClassification.RecommendedRemove,
                IsSystemApp = true,
                UninstallString = uninstCmd,
                QuietUninstallString = uninstCmd.Contains("/silent") ? uninstCmd : $"{uninstCmd} /silent",
                InstallLocation = activeExe != null ? Path.GetDirectoryName(activeExe) ?? "" : "",
                IsSafeToRemove = true,
                IsInstalled = true
            });
        }
        catch { }
    }

    /// <summary>
    /// Mesin Klasifikasi Aplikasi terpusat sesuai Bab 18 & 61 FITUR.md
    /// </summary>
    public static AppClassification ClassifyApp(string displayName, string publisher, string packageName, bool isSystemApp, bool isSafeToRemove)
    {
        string combined = $"{displayName} {publisher} {packageName}".ToLowerInvariant();

        // 1. Keamanan Windows & Inti Sistem (Wajib dijaga)
        if (EssentialCoreSystemPackages.Contains(packageName) ||
            combined.Contains("sechealthui") ||
            combined.Contains("windows defender") ||
            combined.Contains("windows security") ||
            combined.Contains("smartscreen"))
        {
            return AppClassification.Security;
        }

        // 2. Driver & Utilitas Hardware (Touchpad, Audio, GPU, Hotkey)
        if (combined.Contains("realtek") ||
            combined.Contains("synaptics") ||
            combined.Contains("elan trackpad") ||
            combined.Contains("nvidia graphics") ||
            combined.Contains("amd radeon") ||
            combined.Contains("intel graphics") ||
            combined.Contains("audio driver") ||
            combined.Contains("touchpad") ||
            combined.Contains("hotkey service"))
        {
            return AppClassification.Driver;
        }

        // 3. Dependensi Sistem (Visual C++, .NET, WebView2, DirectX)
        if (combined.Contains("visual c++") ||
            combined.Contains("microsoft .net") ||
            combined.Contains("webview2") ||
            combined.Contains("directx"))
        {
            return AppClassification.Dependency;
        }

        // 4. OEM Bloatware (Bab 61: Dell, HP, Lenovo, ASUS, Acer, MSI trialware / telemetry / promo)
        if (combined.Contains("supportassist") ||
            combined.Contains("hp support assistant") ||
            combined.Contains("hp smart") ||
            combined.Contains("lenovo vantage") ||
            combined.Contains("lenovo welcome") ||
            combined.Contains("myasus") ||
            combined.Contains("armoury crate") ||
            combined.Contains("acer care center") ||
            combined.Contains("mcafee") ||
            combined.Contains("norton") ||
            combined.Contains("wildtangent") ||
            combined.Contains("booking.com") ||
            combined.Contains("candy crush") ||
            combined.Contains("tiktok") ||
            combined.Contains("march of empires") ||
            combined.Contains("hidden city") ||
            combined.Contains("caesars slots"))
        {
            return AppClassification.OemBloat;
        }

        // 5. Rekomendasi Copot (Consumer Bloatware & Telemetry bawaan)
        if (combined.Contains("bingnews") ||
            combined.Contains("bingweather") ||
            combined.Contains("solitaire") ||
            combined.Contains("clipchamp") ||
            combined.Contains("mixed reality") ||
            combined.Contains("feedback hub") ||
            combined.Contains("get help") ||
            combined.Contains("get started") ||
            combined.Contains("your phone") ||
            combined.Contains("phonelink") ||
            combined.Contains("onedrive") ||
            combined.Contains("skype") ||
            combined.Contains("3d viewer") ||
            combined.Contains("print 3d"))
        {
            return AppClassification.RecommendedRemove;
        }

        // 6. Aplikasi Pengguna (Third-Party yang diinstall user secara sadar)
        if (!isSystemApp && (combined.Contains("google chrome") ||
                             combined.Contains("mozilla firefox") ||
                             combined.Contains("visual studio") ||
                             combined.Contains("code") ||
                             combined.Contains("discord") ||
                             combined.Contains("telegram") ||
                             combined.Contains("steam") ||
                             combined.Contains("vlc") ||
                             combined.Contains("git") ||
                             combined.Contains("whatsapp")))
        {
            return AppClassification.UserApp;
        }

        if (isSystemApp)
        {
            return isSafeToRemove ? AppClassification.Optional : AppClassification.System;
        }

        return AppClassification.UserApp;
    }

    public async Task<(bool Success, string Message)> UninstallAppAsync(DebloatAppItem app, Action<string>? onProgress = null)
    {
        LoggerService.Instance.Info($"Mencopot aplikasi: {app.DisplayName} ({app.AppTypeDisplay}, {app.OriginBadgeText})...");
        app.IsProcessing = true;

        try
        {
            // 1. Khusus Microsoft Edge Browser (baik stub UWP maupun installer Win32)
            if ((app.DisplayName.Equals("Microsoft Edge", StringComparison.OrdinalIgnoreCase) || 
                 app.PackageName.Contains("MicrosoftEdge", StringComparison.OrdinalIgnoreCase)) &&
                !app.DisplayName.Contains("WebView", StringComparison.OrdinalIgnoreCase))
            {
                var (edgeSuccess, edgeMsg) = await WindowsLiteService.RemoveMicrosoftEdgeAsync();
                if (edgeSuccess)
                {
                    app.IsInstalled = false;
                    return (true, edgeMsg);
                }
                return (false, edgeMsg);
            }

            // 2. Khusus Microsoft OneDrive
            if (app.DisplayName.Contains("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                app.PackageName.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
            {
                var (oneDriveSuccess, oneDriveMsg) = await WindowsLiteService.RemoveOneDriveAsync();
                if (oneDriveSuccess)
                {
                    app.IsInstalled = false;
                    return (true, oneDriveMsg);
                }
                return (false, oneDriveMsg);
            }

            // 3. Eksekusi Permanent Uninstall Engine (Discovery -> Stop Processes -> Stop/Remove Services -> Remove Tasks -> Remove Startup -> Uninstaller -> Residual Cleanup -> Verification)
            var result = await PermanentUninstallEngine.UninstallPermanentlyAsync(app, onProgress);
            if (result.Success || result.Status == UninstallVerificationStatus.PartialRemoval)
            {
                app.IsInstalled = false;
                return (true, result.Message);
            }

            return (false, result.Message);
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

    public async Task<(bool Success, string Message)> ForceUninstallAppAsync(DebloatAppItem app, Action<string>? onProgress = null)
    {
        LoggerService.Instance.Warning($"[FORCE UNINSTALL] Memulai prosedur paksa copot: {app.DisplayName} ({app.AppTypeDisplay}, {app.OriginBadgeText})...");
        app.IsProcessing = true;

        try
        {
            // 1. Khusus Microsoft Edge Browser
            if ((app.DisplayName.Equals("Microsoft Edge", StringComparison.OrdinalIgnoreCase) || 
                 app.PackageName.Contains("MicrosoftEdge", StringComparison.OrdinalIgnoreCase)) &&
                !app.DisplayName.Contains("WebView", StringComparison.OrdinalIgnoreCase))
            {
                var (edgeSuccess, edgeMsg) = await WindowsLiteService.RemoveMicrosoftEdgeAsync();
                if (edgeSuccess)
                {
                    app.IsInstalled = false;
                    return (true, edgeMsg);
                }
            }

            // 2. Khusus Microsoft OneDrive
            if (app.DisplayName.Contains("OneDrive", StringComparison.OrdinalIgnoreCase) ||
                app.PackageName.Contains("OneDrive", StringComparison.OrdinalIgnoreCase))
            {
                var (oneDriveSuccess, oneDriveMsg) = await WindowsLiteService.RemoveOneDriveAsync();
                if (oneDriveSuccess)
                {
                    app.IsInstalled = false;
                    return (true, oneDriveMsg);
                }
            }

            // 3. Eksekusi Force Uninstall Engine (Bypass broken uninstaller, aggressive process/service/task/takeown/registry purge)
            var result = await PermanentUninstallEngine.ForceUninstallPermanentlyAsync(app, onProgress);
            if (result.Success || result.Status == UninstallVerificationStatus.PartialRemoval)
            {
                app.IsInstalled = false;
                return (true, result.Message);
            }

            return (false, result.Message);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.Error($"Gagal paksa copot {app.DisplayName}: {ex.Message}");
            return (false, $"Error saat paksa uninstall: {ex.Message}");
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
