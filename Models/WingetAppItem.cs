using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AturOS.Models;

public class WingetAppItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _status = "Siap dipasang";
    private bool _isInstalling;

    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Utilitas"; // Runtimes, Utilitas, Peramban & Gaming
    public string Description { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public bool IsInstalling
    {
        get => _isInstalling;
        set { _isInstalling = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
