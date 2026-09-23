using AturOS.Helpers;
using Microsoft.Win32;

namespace AturOS.Services;

public class ExplorerTweaksService
{
    public bool GetShowFileExtensions()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            var val = key?.GetValue("HideFileExt");
            return val is int i && i == 0;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetShowFileExtensionsAsync(bool show)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                key.SetValue("HideFileExt", show ? 0 : 1, RegistryValueKind.DWord);
                return (true, $"Ekstensi nama file sekarang {(show ? "Ditampilkan" : "Disembunyikan")}.");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public bool GetShowHiddenFiles()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            var val = key?.GetValue("Hidden");
            return val is int i && i == 1;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetShowHiddenFilesAsync(bool show)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                key.SetValue("Hidden", show ? 1 : 2, RegistryValueKind.DWord);
                return (true, $"File dan folder tersembunyi sekarang {(show ? "Ditampilkan" : "Disembunyikan")}.");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public bool GetRemoveShortcutSuffix()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates");
            var val = key?.GetValue("ShortcutNameTemplate");
            return val != null && val.ToString() == "%s.lnk";
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetRemoveShortcutSuffixAsync(bool remove)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates");
                if (remove)
                {
                    key.SetValue("ShortcutNameTemplate", "%s.lnk", RegistryValueKind.String);
                    // Also binary link = 00 00 00 00
                    using var expKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer");
                    expKey.SetValue("link", new byte[] { 0, 0, 0, 0 }, RegistryValueKind.Binary);
                }
                else
                {
                    key.DeleteValue("ShortcutNameTemplate", false);
                }
                return (true, $"Kata '- Shortcut' atau '- Pintasan' {(remove ? "dihapus" : "dikembalikan")} saat membuat pintasan baru.");
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public bool GetTakeOwnershipStatus()
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(@"*\shell\runas");
            return key != null;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetTakeOwnershipAsync(bool enable)
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");

            try
            {
                if (enable)
                {
                    // Files
                    using var fileKey = Registry.ClassesRoot.CreateSubKey(@"*\shell\runas");
                    fileKey.SetValue("", "Take Ownership");
                    fileKey.SetValue("NoWorkingDirectory", "");
                    using var fileCmd = fileKey.CreateSubKey("command");
                    fileCmd.SetValue("", "cmd.exe /c takeown /f \"%1\" && icacls \"%1\" /grant administrators:F");
                    fileCmd.SetValue("IsolatedCommand", "cmd.exe /c takeown /f \"%1\" && icacls \"%1\" /grant administrators:F");

                    // Directories
                    using var dirKey = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\runas");
                    dirKey.SetValue("", "Take Ownership");
                    dirKey.SetValue("NoWorkingDirectory", "");
                    using var dirCmd = dirKey.CreateSubKey("command");
                    dirCmd.SetValue("", "cmd.exe /c takeown /f \"%1\" /r /d y && icacls \"%1\" /grant administrators:F /t");
                    dirCmd.SetValue("IsolatedCommand", "cmd.exe /c takeown /f \"%1\" /r /d y && icacls \"%1\" /grant administrators:F /t");

                    return (true, "Menu 'Take Ownership' berhasil ditambahkan pada klik-kanan file dan folder.");
                }
                else
                {
                    Registry.ClassesRoot.DeleteSubKeyTree(@"*\shell\runas", false);
                    Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\runas", false);
                    return (true, "Menu 'Take Ownership' berhasil dihapus.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public bool GetDisableSecurityWarning()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Associations");
            var val = key?.GetValue("LowRiskFileTypes");
            return val != null && val.ToString()!.Contains(".exe");
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetDisableSecurityWarningAsync(bool disable)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Associations");
                if (disable)
                {
                    key.SetValue("LowRiskFileTypes", ".exe;.bat;.cmd;.vbs;.ps1;.msi;.reg", RegistryValueKind.String);
                    return (true, "Peringatan 'Open File - Security Warning' dinonaktifkan untuk file unduhan.");
                }
                else
                {
                    key.DeleteValue("LowRiskFileTypes", false);
                    return (true, "Peringatan 'Open File - Security Warning' dikembalikan ke default.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public bool IsWindows11 { get; }

    public ExplorerTweaksService()
    {
        var (_, build) = SystemInfoService.GetOperatingSystemDetails();
        IsWindows11 = build.Contains("Build") && int.TryParse(build.Replace("Build", "").Trim(), out int b) && b >= 22000;
    }

    public bool GetClassicContextMenuStatus()
    {
        if (!IsWindows11) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32");
            return key != null;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetClassicContextMenuAsync(bool enable)
    {
        return await Task.Run(() =>
        {
            if (!IsWindows11) return (false, "Menu Konteks Klasik hanya berlaku untuk Windows 11.");
            try
            {
                if (enable)
                {
                    using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32");
                    key.SetValue("", "");
                    return (true, "Menu Konteks Klasik Windows 10 berhasil diaktifkan. Muat ulang Explorer untuk melihat perubahan.");
                }
                else
                {
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", false);
                    return (true, "Menu Konteks modern default Windows 11 dikembalikan. Muat ulang Explorer untuk melihat perubahan.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Gagal mengubah menu konteks: {ex.Message}");
            }
        });
    }

    public bool GetOpenTerminalAdminStatus()
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(@"Directory\Background\shell\OpenTerminalAdmin");
            return key != null;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetOpenTerminalAdminAsync(bool enable)
    {
        return await Task.Run(() =>
        {
            if (!AdministratorHelper.IsAdministrator) return (false, "Membutuhkan hak Administrator.");
            try
            {
                string[] locations = new[] { @"Directory\Background\shell\OpenTerminalAdmin", @"Directory\shell\OpenTerminalAdmin" };
                if (enable)
                {
                    foreach (var loc in locations)
                    {
                        using var key = Registry.ClassesRoot.CreateSubKey(loc);
                        key.SetValue("", "Buka Terminal sebagai Administrator");
                        key.SetValue("Icon", "wt.exe");
                        key.SetValue("HasLUAShield", "");
                        using var cmdKey = key.CreateSubKey("command");
                        cmdKey.SetValue("", "powershell.exe -Command \"Start-Process wt.exe -ArgumentList '-d', '\"\"%V\"\"' -Verb RunAs\"");
                    }
                    return (true, "Opsi 'Buka Terminal sebagai Administrator' berhasil ditambahkan ke menu klik-kanan.");
                }
                else
                {
                    foreach (var loc in locations)
                    {
                        Registry.ClassesRoot.DeleteSubKeyTree(loc, false);
                    }
                    return (true, "Opsi 'Buka Terminal sebagai Administrator' berhasil dihapus dari menu klik-kanan.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public bool GetDisableBingSearchStatus()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Policies\Microsoft\Windows\Explorer");
            var val = key?.GetValue("DisableSearchBoxSuggestions");
            return val is int i && i == 1;
        }
        catch { return false; }
    }

    public async Task<(bool Success, string Message)> SetDisableBingSearchAsync(bool disable)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Policies\Microsoft\Windows\Explorer");
                if (disable)
                {
                    key.SetValue("DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord);
                    return (true, "Pencarian web Bing di Start Menu dinonaktifkan (pencarian file lokal lebih cepat).");
                }
                else
                {
                    key.DeleteValue("DisableSearchBoxSuggestions", false);
                    return (true, "Pencarian web Bing di Start Menu dikembalikan ke default.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }
}
