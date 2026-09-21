using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AturOS.Models;

public class DnsPresetItem : INotifyPropertyChanged
{
    private long _pingMs = -1;
    private bool _isTesting;

    public string Name { get; set; } = string.Empty;
    public string PrimaryDns { get; set; } = string.Empty;
    public string SecondaryDns { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public long PingMs
    {
        get => _pingMs;
        set
        {
            _pingMs = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PingDisplay));
        }
    }

    public bool IsTesting
    {
        get => _isTesting;
        set { _isTesting = value; OnPropertyChanged(); }
    }

    public string PingDisplay => PingMs >= 0 ? $"{PingMs} ms" : (_isTesting ? "Menguji..." : "-");

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
