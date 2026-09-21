namespace AturOS.Models;

public class TopProcessItem
{
    public int Id { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public long WorkingSetBytes { get; set; }
    public double WorkingSetMb => (double)WorkingSetBytes / (1024 * 1024);
    public string FormattedMemory => $"{WorkingSetMb:F1} MB";
}
