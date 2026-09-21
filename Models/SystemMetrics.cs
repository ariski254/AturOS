namespace AturOS.Models;

public class SystemMetrics
{
    public double CpuUsagePercent { get; set; }
    public double TotalRamGb { get; set; }
    public double UsedRamGb { get; set; }
    public double FreeRamGb { get; set; }
    public double RamUsagePercent { get; set; }

    public string SystemDriveLetter { get; set; } = "C:\\";
    public double TotalDriveGb { get; set; }
    public double FreeDriveGb { get; set; }
    public double UsedDriveGb { get; set; }
    public double DriveUsagePercent { get; set; }

    public string OsName { get; set; } = string.Empty;
    public string OsBuild { get; set; } = string.Empty;
    public string Architecture { get; set; } = "x64";
    public bool IsAdministrator { get; set; }
    public string ComputerName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UptimeFormatted { get; set; } = string.Empty;
}
