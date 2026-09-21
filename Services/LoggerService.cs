using System.Collections.Concurrent;
using System.IO;

namespace AturOS.Services;

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Success
}

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Formatted => $"[{Timestamp:HH:mm:ss}] [{Level.ToString().ToUpper()}] {Message}";
}

public class LoggerService
{
    private static readonly Lazy<LoggerService> _instance = new(() => new LoggerService());
    public static LoggerService Instance => _instance.Value;

    public ConcurrentQueue<LogEntry> Entries { get; } = new();
    public event Action<LogEntry>? LogAdded;

    private static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AturOS");
    private static readonly string LogFilePath = Path.Combine(LogDir, "aturos.log");
    private static readonly object _fileLock = new();

    private LoggerService()
    {
        try
        {
            if (!Directory.Exists(LogDir))
            {
                Directory.CreateDirectory(LogDir);
            }
        }
        catch { }
    }

    public void Log(LogLevel level, string message)
    {
        var entry = new LogEntry { Level = level, Message = message };
        Entries.Enqueue(entry);
        
        // Keep maximum 500 entries in memory
        while (Entries.Count > 500 && Entries.TryDequeue(out _)) { }

        // Write to persistent disk log
        try
        {
            lock (_fileLock)
            {
                File.AppendAllText(LogFilePath, $"{entry.Formatted}{Environment.NewLine}");
            }
        }
        catch { }

        LogAdded?.Invoke(entry);
    }

    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message) => Log(LogLevel.Error, message);
    public void Success(string message) => Log(LogLevel.Success, message);
}
