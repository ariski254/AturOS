using System;
using System.Collections.Generic;

namespace AturOS.Models;

public enum UninstallVerificationStatus
{
    NotStarted,
    PermanentlyRemoved,
    RemovedWithPreservedData,
    PartialRemoval,
    Failed
}

public class AppProcessItem
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
}

public class AppServiceItem
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
}

public class AppScheduledTaskItem
{
    public string TaskName { get; set; } = string.Empty;
    public string TaskPath { get; set; } = string.Empty;
    public string ActionExecute { get; set; } = string.Empty;
}

public class AppStartupItem
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
}

public class AppComponentsDiscoveryResult
{
    public DebloatAppItem App { get; set; } = new();
    public List<AppProcessItem> Processes { get; set; } = new();
    public List<AppServiceItem> Services { get; set; } = new();
    public List<AppScheduledTaskItem> ScheduledTasks { get; set; } = new();
    public List<AppStartupItem> StartupEntries { get; set; } = new();
    public List<string> ShortcutFiles { get; set; } = new();
    public List<string> ResidualDirectories { get; set; } = new();
    public List<string> RegistryKeys { get; set; } = new();
    public List<string> UserDataFoldersToPreserve { get; set; } = new();

    public bool HasRunningComponents => Processes.Count > 0 || Services.Count > 0;
    public int TotalComponentsCount => Processes.Count + Services.Count + ScheduledTasks.Count +
                                       StartupEntries.Count + ShortcutFiles.Count + ResidualDirectories.Count;
}

public class PermanentUninstallResult
{
    public DebloatAppItem App { get; set; } = new();
    public UninstallVerificationStatus Status { get; set; } = UninstallVerificationStatus.NotStarted;
    public bool Success => Status == UninstallVerificationStatus.PermanentlyRemoved ||
                           Status == UninstallVerificationStatus.RemovedWithPreservedData;
    public string Message { get; set; } = string.Empty;
    public int ProcessesKilled { get; set; }
    public int ServicesRemoved { get; set; }
    public int ScheduledTasksRemoved { get; set; }
    public int StartupEntriesRemoved { get; set; }
    public int ShortcutsRemoved { get; set; }
    public int DirectoriesPurged { get; set; }
    public int RegistryKeysCleaned { get; set; }
    public bool RequiresRestart { get; set; }
    public List<string> LogEntries { get; set; } = new();
    public DateTime CompletedAt { get; set; } = DateTime.Now;
}
