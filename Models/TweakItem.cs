using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AturOS.Models;

public enum TweakStatus
{
    Aktif,
    Nonaktif,
    TidakDidukung,
    TidakDiketahui
}

public class TweakItem : INotifyPropertyChanged
{
    private TweakStatus _status = TweakStatus.TidakDiketahui;
    private bool _isProcessing;
    private string _statusMessage = string.Empty;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Umum";
    public bool RequiresElevation { get; set; }
    public bool RequiresExplorerRestart { get; set; }

    public TweakStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(CanRestore));
        }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            _isProcessing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(CanRestore));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public string StatusDisplay => Status switch
    {
        TweakStatus.Aktif => "Aktif",
        TweakStatus.Nonaktif => "Nonaktif",
        TweakStatus.TidakDidukung => "Tidak Didukung",
        _ => "Tidak Diketahui"
    };

    public bool CanApply => !IsProcessing && Status != TweakStatus.Aktif && Status != TweakStatus.TidakDidukung;
    public bool CanRestore => !IsProcessing && Status != TweakStatus.Nonaktif && Status != TweakStatus.TidakDidukung;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
