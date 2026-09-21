using AturOS.Helpers;
using AturOS.Models;
using Microsoft.Win32;

namespace AturOS.Services;

public class RegistryTweakService
{
    private readonly bool _isWindows11;

    public RegistryTweakService()
    {
        var (_, build) = SystemInfoService.GetOperatingSystemDetails();
        _isWindows11 = build.Contains("Build") && int.TryParse(build.Replace("Build", "").Trim(), out int b) && b >= 22000;
    }

    public List<TweakItem> GetAvailableTweaks()
    {
        return new List<TweakItem>
        {
            new TweakItem
            {
                Id = "classic_context_menu",
                Name = "Menu Konteks Klasik (Windows 11)",
                Description = "Mengembalikan menu klik kanan klasik gaya Windows 10 tanpa tombol 'Show more options'.",
                Category = "Tampilan",
                RequiresElevation = false,
                RequiresExplorerRestart = true
            },
            new TweakItem
            {
                Id = "disable_widgets",
                Name = "Nonaktifkan Windows Widgets",
                Description = "Mematikan fitur Widget / News & Interests pada taskbar untuk menghemat RAM dan CPU background.",
                Category = "Kustomisasi",
                RequiresElevation = true,
                RequiresExplorerRestart = true
            },
            new TweakItem
            {
                Id = "disable_copilot",
                Name = "Nonaktifkan Windows Copilot",
                Description = "Menyembunyikan dan menonaktifkan integrasi tombol Windows Copilot pada taskbar.",
                Category = "Debloat",
                RequiresElevation = false,
                RequiresExplorerRestart = true
            },
            new TweakItem
            {
                Id = "disable_bing_search",
                Name = "Nonaktifkan Bing di Start Menu",
                Description = "Menghilangkan hasil pencarian web Bing saat mengetik di Start Menu, mempercepat pencarian file lokal.",
                Category = "Kustomisasi",
                RequiresElevation = false,
                RequiresExplorerRestart = true
            }
        };
    }

    public void RefreshTweakStatus(TweakItem tweak)
    {
        try
        {
            switch (tweak.Id)
            {
                case "classic_context_menu":
                    if (!_isWindows11)
                    {
                        tweak.Status = TweakStatus.TidakDidukung;
                        tweak.StatusMessage = "Hanya untuk Windows 11";
                        return;
                    }

                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"))
                    {
                        tweak.Status = key != null ? TweakStatus.Aktif : TweakStatus.Nonaktif;
                    }
                    break;

                case "disable_widgets":
                    if (_isWindows11)
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Dsh");
                        var val = key?.GetValue("AllowNewsAndInterests");
                        tweak.Status = (val is int intVal && intVal == 0) ? TweakStatus.Aktif : TweakStatus.Nonaktif;
                    }
                    else
                    {
                        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds");
                        var val = key?.GetValue("ShellFeedsTaskbarViewMode");
                        tweak.Status = (val is int intVal && intVal == 2) ? TweakStatus.Aktif : TweakStatus.Nonaktif;
                    }
                    break;

                case "disable_copilot":
                    if (!_isWindows11)
                    {
                        tweak.Status = TweakStatus.TidakDidukung;
                        tweak.StatusMessage = "Hanya untuk Windows 11";
                        return;
                    }

                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\WindowsCopilot"))
                    {
                        var val = key?.GetValue("TurnOffWindowsCopilot");
                        tweak.Status = (val is int intVal && intVal == 1) ? TweakStatus.Aktif : TweakStatus.Nonaktif;
                    }
                    break;

                case "disable_bing_search":
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\Explorer"))
                    {
                        var val = key?.GetValue("DisableSearchBoxSuggestions");
                        tweak.Status = (val is int intVal && intVal == 1) ? TweakStatus.Aktif : TweakStatus.Nonaktif;
                    }
                    break;

                default:
                    tweak.Status = TweakStatus.TidakDiketahui;
                    break;
            }
        }
        catch (Exception ex)
        {
            tweak.Status = TweakStatus.TidakDiketahui;
            tweak.StatusMessage = ex.Message;
            LoggerService.Instance.Warning($"Cek status tweak {tweak.Name} gagal: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> ApplyTweakAsync(TweakItem tweak)
    {
        return await Task.Run(() =>
        {
            try
            {
                switch (tweak.Id)
                {
                    case "classic_context_menu":
                        if (!_isWindows11) return (false, "Fitur ini hanya didukung pada Windows 11.");

                        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"))
                        {
                            key.SetValue("", ""); // Set default value to empty string
                        }
                        RefreshTweakStatus(tweak);
                        LoggerService.Instance.Success("Menu Konteks Klasik berhasil diterapkan.");
                        return (true, "Menu Konteks Klasik diterapkan. Restart Explorer untuk melihat efek.");

                    case "disable_widgets":
                        if (!AdministratorHelper.IsAdministrator)
                            return (false, "Mengubah pengaturan Widgets membutuhkan hak Administrator.");

                        if (_isWindows11)
                        {
                            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Dsh");
                            key.SetValue("AllowNewsAndInterests", 0, RegistryValueKind.DWord);
                        }
                        else
                        {
                            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds");
                            key.SetValue("ShellFeedsTaskbarViewMode", 2, RegistryValueKind.DWord);
                        }
                        RefreshTweakStatus(tweak);
                        LoggerService.Instance.Success("Windows Widgets dinonaktifkan.");
                        return (true, "Widgets dinonaktifkan.");

                    case "disable_copilot":
                        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\WindowsCopilot"))
                        {
                            key.SetValue("TurnOffWindowsCopilot", 1, RegistryValueKind.DWord);
                        }
                        RefreshTweakStatus(tweak);
                        LoggerService.Instance.Success("Windows Copilot dinonaktifkan.");
                        return (true, "Windows Copilot dinonaktifkan.");

                    case "disable_bing_search":
                        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\Explorer"))
                        {
                            key.SetValue("DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord);
                        }
                        RefreshTweakStatus(tweak);
                        LoggerService.Instance.Success("Bing Search di Start Menu dinonaktifkan.");
                        return (true, "Bing Search Start Menu dinonaktifkan.");

                    default:
                        return (false, "Tweak tidak dikenal.");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Gagal menerapkan {tweak.Name}: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public async Task<(bool Success, string Message)> RestoreTweakAsync(TweakItem tweak)
    {
        return await Task.Run(() =>
        {
            try
            {
                switch (tweak.Id)
                {
                    case "classic_context_menu":
                        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", false);
                        RefreshTweakStatus(tweak);
                        LoggerService.Instance.Success("Menu Konteks Klasik dikembalikan ke default Windows 11.");
                        return (true, "Menu konteks dikembalikan ke default Windows 11.");

                    case "disable_widgets":
                        if (!AdministratorHelper.IsAdministrator)
                            return (false, "Mengubah pengaturan Widgets membutuhkan hak Administrator.");

                        if (_isWindows11)
                        {
                            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Dsh", true);
                            key?.DeleteValue("AllowNewsAndInterests", false);
                        }
                        else
                        {
                            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Feeds", true);
                            key?.DeleteValue("ShellFeedsTaskbarViewMode", false);
                        }
                        RefreshTweakStatus(tweak);
                        LoggerService.Instance.Success("Pengaturan Widgets dikembalikan ke default.");
                        return (true, "Widgets dikembalikan ke default.");

                    case "disable_copilot":
                        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\WindowsCopilot", true))
                        {
                            key?.DeleteValue("TurnOffWindowsCopilot", false);
                        }
                        RefreshTweakStatus(tweak);
                        LoggerService.Instance.Success("Pengaturan Copilot dikembalikan ke default.");
                        return (true, "Copilot dikembalikan ke default.");

                    case "disable_bing_search":
                        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\Explorer", true))
                        {
                            key?.DeleteValue("DisableSearchBoxSuggestions", false);
                        }
                        RefreshTweakStatus(tweak);
                        LoggerService.Instance.Success("Bing Search Start Menu dikembalikan ke default.");
                        return (true, "Bing Search dikembalikan ke default.");

                    default:
                        return (false, "Tweak tidak dikenal.");
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Gagal mengembalikan {tweak.Name}: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public static async Task<bool> RestartExplorerAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                LoggerService.Instance.Info("Merestart Windows Explorer...");
                foreach (var process in System.Diagnostics.Process.GetProcessesByName("explorer"))
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                    catch { }
                }

                Thread.Sleep(500);
                System.Diagnostics.Process.Start("explorer.exe");
                LoggerService.Instance.Success("Windows Explorer berhasil direstart.");
                return true;
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Gagal merestart Explorer: {ex.Message}");
                return false;
            }
        });
    }
}
