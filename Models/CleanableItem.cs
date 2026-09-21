using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AturOS.Models;

public class CleanableItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private long _sizeBytes;
    private int _fileCount;
    private string _status = "Belum di-scan";

    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            _sizeBytes = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedSize));
        }
    }

    public int FileCount
    {
        get => _fileCount;
        set { _fileCount = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string FormattedSize
    {
        get
        {
            if (SizeBytes <= 0) return "0 MB";
            if (SizeBytes >= 1024L * 1024 * 1024)
                return $"{(double)SizeBytes / (1024 * 1024 * 1024):F2} GB";
            return $"{(double)SizeBytes / (1024 * 1024):F2} MB";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
