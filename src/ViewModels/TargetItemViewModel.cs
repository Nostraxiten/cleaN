using CleaN.Core;

namespace CleaN.ViewModels;

/// <summary>A single reviewable item in a confirmation list.</summary>
public sealed class TargetItemViewModel : ObservableObject
{
    private readonly Action? _selectionChanged;
    private bool _isSelected = true;

    public TargetItemViewModel(CleanTarget target, Action? selectionChanged = null)
    {
        Target = target;
        _selectionChanged = selectionChanged;
    }

    public CleanTarget Target { get; }

    public string Path => Target.Path;

    public string Note => Target.Note ?? string.Empty;

    public string SizeText => Target.SizeBytes > 0 ? SizeFormatter.Format(Target.SizeBytes) : string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _selectionChanged?.Invoke();
            }
        }
    }
}

/// <summary>A location the empty-folder scan can be pointed at.</summary>
public sealed class ScanRootViewModel : ObservableObject
{
    private readonly Action _changed;
    private bool _isSelected;

    public ScanRootViewModel(string path, string label, bool isSelected, Action changed)
    {
        Path = path;
        Label = label;
        _isSelected = isSelected;
        _changed = changed;
    }

    public string Path { get; }

    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _changed();
            }
        }
    }
}
