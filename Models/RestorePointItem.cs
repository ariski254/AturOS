namespace AturOS.Models;

public class RestorePointItem
{
    public int SequenceNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CreationTime { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
}
