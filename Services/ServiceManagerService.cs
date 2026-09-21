using System.ServiceProcess;
using AturOS.Helpers;
using AturOS.Models;

namespace AturOS.Services;

public class ServiceManagerService
{
    private const string DiagTrackServiceName = "DiagTrack";

    public TweakItem GetTelemetryTweakItem()
    {
        return new TweakItem
        {
            Id = "service_diagtrack",
            Name = "Layanan Telemetri Windows (DiagTrack)",
            Description = "Menghentikan dan menonaktifkan Connected User Experiences and Telemetry untuk mengurangi pengiriman data latar belakang ke server Microsoft.",
            Category = "Privasi & Layanan",
            RequiresElevation = true,
            RequiresExplorerRestart = false
        };
    }

    public void RefreshTelemetryStatus(TweakItem tweak)
    {
        try
        {
            var services = ServiceController.GetServices();
            var diagTrack = services.FirstOrDefault(s => s.ServiceName.Equals(DiagTrackServiceName, StringComparison.OrdinalIgnoreCase));

            if (diagTrack == null)
            {
                tweak.Status = TweakStatus.TidakDidukung;
                tweak.StatusMessage = "Layanan DiagTrack tidak ditemukan di sistem ini.";
                return;
            }

            // Note: If service is running, it's considered "Nonaktif" in terms of debloat tweak (meaning telemetry is still running)
            // But from a tweak perspective: "Aktif" means tweak is active (telemetry disabled).
            // Let's make it intuitive:
            // If DiagTrack is Running or StartType is not Disabled -> Telemetry is active -> Tweak is Nonaktif.
            // If DiagTrack is Stopped/Disabled -> Telemetry is blocked -> Tweak is Aktif.
            bool isStopped = diagTrack.Status == ServiceControllerStatus.Stopped;

            using var reg = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\DiagTrack");
            var startVal = reg?.GetValue("Start");
            bool isDisabled = (startVal is int intVal && intVal == 4);

            if (isDisabled && isStopped)
            {
                tweak.Status = TweakStatus.Aktif;
                tweak.StatusMessage = "Telemetri dinonaktifkan (Layanan Berhenti & Dimatikan)";
            }
            else
            {
                tweak.Status = TweakStatus.Nonaktif;
                tweak.StatusMessage = $"Telemetri aktif (Status: {diagTrack.Status})";
            }
        }
        catch (Exception ex)
        {
            tweak.Status = TweakStatus.TidakDiketahui;
            tweak.StatusMessage = ex.Message;
            LoggerService.Instance.Warning($"Cek status DiagTrack gagal: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> DisableTelemetryAsync(TweakItem tweak)
    {
        return await Task.Run(async () =>
        {
            if (!AdministratorHelper.IsAdministrator)
            {
                return (false, "Mematikan layanan DiagTrack membutuhkan hak akses Administrator.");
            }

            try
            {
                LoggerService.Instance.Info("Menonaktifkan layanan DiagTrack...");

                // 1. Configure startup type to disabled via sc.exe
                var configResult = await ProcessHelper.RunCommandAsync("sc.exe", "config DiagTrack start=disabled");

                // 2. Stop service if running
                try
                {
                    using var sc = new ServiceController(DiagTrackServiceName);
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                    }
                }
                catch { }

                RefreshTelemetryStatus(tweak);
                LoggerService.Instance.Success("Layanan DiagTrack berhasil dinonaktifkan.");
                return (true, "Telemetri Windows (DiagTrack) berhasil dinonaktifkan.");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Gagal menonaktifkan DiagTrack: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        });
    }

    public async Task<(bool Success, string Message)> RestoreTelemetryAsync(TweakItem tweak)
    {
        return await Task.Run(async () =>
        {
            if (!AdministratorHelper.IsAdministrator)
            {
                return (false, "Mengembalikan layanan DiagTrack membutuhkan hak akses Administrator.");
            }

            try
            {
                LoggerService.Instance.Info("Mengembalikan layanan DiagTrack ke otomatis...");

                // 1. Configure startup type to auto
                await ProcessHelper.RunCommandAsync("sc.exe", "config DiagTrack start=auto");

                // 2. Start service
                try
                {
                    using var sc = new ServiceController(DiagTrackServiceName);
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        sc.Start();
                    }
                }
                catch { }

                RefreshTelemetryStatus(tweak);
                LoggerService.Instance.Success("Layanan DiagTrack berhasil dikembalikan ke default.");
                return (true, "Telemetri Windows (DiagTrack) dikembalikan ke default.");
            }
            catch (Exception ex)
            {
                LoggerService.Instance.Error($"Gagal mengembalikan DiagTrack: {ex.Message}");
                return (false, $"Error: {ex.Message}");
            }
        });
    }
}
