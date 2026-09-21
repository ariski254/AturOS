using System.Diagnostics;
using System.Security.Principal;

namespace AturOS.Helpers;

public static class AdministratorHelper
{
    private static readonly Lazy<bool> _isAdmin = new(() =>
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    });

    public static bool IsAdministrator => _isAdmin.Value;

    public static bool RestartAsAdministrator()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath)) return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            System.Windows.Application.Current.Shutdown();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
