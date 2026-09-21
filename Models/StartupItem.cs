namespace AturOS.Models;

public class StartupItem
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty; // HKCU, HKLM, Folder
    public bool IsEnabled { get; set; } = true;
}
