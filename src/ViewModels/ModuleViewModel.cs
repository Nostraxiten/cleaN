using CleaN.Core;
using CleaN.Modules;

namespace CleaN.ViewModels;

/// <summary>One cleaning module as shown in a list: a checkbox, a description and its findings.</summary>
public sealed class ModuleViewModel : ObservableObject
{
    private readonly Action _selectionChanged;
    private readonly Action _resultChanged;
    private bool _isSelected;
    private ScanResult? _result;
    private bool _isScanning;

    public ModuleViewModel(ICleanerModule module, bool isSelected, Action selectionChanged, Action resultChanged)
    {
        Module = module;
        _isSelected = isSelected;
        _selectionChanged = selectionChanged;
        _resultChanged = resultChanged;
    }

    public ICleanerModule Module { get; }

    public string Id => Module.Id;

    public string Name => Module.Name;

    public string Description => Module.Description;

    public bool RequiresElevation => Module.RequiresElevation;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _selectionChanged();
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public ScanResult? Result
    {
        get => _result;
        set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(HasFindings));
                OnPropertyChanged(nameof(WarningText));
                OnPropertyChanged(nameof(HasWarnings));
                _resultChanged();
            }
        }
    }

    public bool HasFindings => Result is { ItemCount: > 0 };

    public long SelectedBytes => IsSelected && Result is not null ? Result.TotalBytes : 0;

    public int SelectedItems => IsSelected && Result is not null ? Result.ItemCount : 0;

    public string StatusText
    {
        get
        {
            if (IsScanning)
            {
                return "Analyzing...";
            }

            if (Result is null)
            {
                return "Not analyzed yet";
            }

            return Result.ItemCount == 0
                ? "Nothing to clean"
                : $"{Result.ItemCount:N0} item(s) - {SizeFormatter.Format(Result.TotalBytes)}";
        }
    }

    public bool HasWarnings => Result is { Warnings.Count: > 0 };

    public string WarningText => Result is null ? string.Empty : string.Join(Environment.NewLine, Result.Warnings);

    public void Reset()
    {
        Result = null;
    }
}
