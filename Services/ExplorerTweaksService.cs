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
                    return (true, "Peringatan keamanan file unduhan dikembalikan ke default.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error: {ex.Message}");
            }
        });
    }
}
