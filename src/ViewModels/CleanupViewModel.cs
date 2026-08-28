using System.Collections.ObjectModel;
using CleaN.Core;
using CleaN.Modules;

namespace CleaN.ViewModels;

/// <summary>
/// Drives one tab's worth of modules: analyze, review, clean. The two steps are always
/// separate, so the user sees the list before anything is removed.
/// </summary>
public sealed class CleanupViewModel : ObservableObject
{
    private readonly MainViewModel _shell;
    private CancellationTokenSource? _cancellation;
    private string _statusText = "Press Analyze to see what can be freed.";
    private string _currentPath = string.Empty;
    private bool _isBusy;
    private bool _hasResults;

    public CleanupViewModel(MainViewModel shell, string title, string subtitle, IEnumerable<ICleanerModule> modules)
    {
        _shell = shell;
        Title = title;
        Subtitle = subtitle;

        foreach (var module in modules)
        {
            Modules.Add(new ModuleViewModel(module, shell.IsModuleSelected(module), OnSelectionChanged,
                OnResultChanged));
        }

        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsBusy && Modules.Any(module => module.IsSelected));
        CleanCommand = new AsyncRelayCommand(CleanAsync, () => !IsBusy && HasResults);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        SelectAllCommand = new RelayCommand(() => SetAllSelected(true));
        SelectNoneCommand = new RelayCommand(() => SetAllSelected(false));
    }

    public string Title { get; }

    public string Subtitle { get; }

    public ObservableCollection<ModuleViewModel> Modules { get; } = new();

    public AsyncRelayCommand AnalyzeCommand { get; }

    public AsyncRelayCommand CleanCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand SelectAllCommand { get; }

    public RelayCommand SelectNoneCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommands();
            }
        }
    }

    public bool HasResults
    {
        get => _hasResults;
        private set
        {
            if (SetProperty(ref _hasResults, value))
            {
                RefreshCommands();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>The file currently being processed, shown while a clean run is in progress.</summary>
    public string CurrentPath
    {
        get => _currentPath;
        private set => SetProperty(ref _currentPath, value);
    }

    /// <summary>Label of the primary button, which changes with the preview switch.</summary>
    public string CleanButtonText => _shell.PreviewOnly ? "Simulate cleaning" : "Clean now";

    /// <summary>
    /// Spells out why the button simulates instead of deleting, and where the switch is.
    /// Without this the preview default looks like the app refusing to do its job.
    /// </summary>
    public string ModeHint => _shell.PreviewOnly
        ? "Preview mode is on, so this only simulates. Untick \u201cPreview mode\u201d at the top right to delete for real."
        : "Preview mode is off: the selected items will be deleted permanently.";

    public void OnPreviewModeChanged()
    {
        OnPropertyChanged(nameof(CleanButtonText));
        OnPropertyChanged(nameof(ModeHint));
    }

    private async Task AnalyzeAsync()
    {
        IsBusy = true;
        HasResults = false;
        CurrentPath = string.Empty;
        _cancellation = new CancellationTokenSource();

        var selected = Modules.Where(module => module.IsSelected).ToList();
        foreach (var module in Modules)
        {
            module.Reset();
        }

        try
        {
            foreach (var module in selected)
            {
                module.IsScanning = true;
                StatusText = $"Analyzing {module.Name}...";
                try
                {
                    module.Result = await module.Module.ScanAsync(_cancellation.Token).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    module.Result = new ScanResult(module.Id, Array.Empty<CleanTarget>(),
                        new[] { $"The analysis failed: {ex.Message}" });
                }
                finally
                {
                    module.IsScanning = false;
                }
            }

            HasResults = Modules.Any(module => module.HasFindings);
            StatusText = HasResults
                ? $"Analysis finished: {TotalSelectedItems:N0} item(s), {SizeFormatter.Format(TotalSelectedBytes)} can be freed."
                : "Analysis finished: nothing to clean here.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analysis cancelled.";
        }
        finally
        {
            foreach (var module in Modules)
            {
                module.IsScanning = false;
            }

            IsBusy = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private async Task CleanAsync()
    {
        var preview = _shell.PreviewOnly;
        var modules = Modules.Where(module => module.IsSelected && module.HasFindings).ToList();
        if (modules.Count == 0)
        {
            StatusText = "There is nothing selected to clean.";
            return;
        }

        if (!preview && !_shell.Dialogs.Confirm("Delete these items?",
                $"cleaN is about to permanently delete {TotalSelectedItems:N0} item(s) " +
                $"and free {SizeFormatter.Format(TotalSelectedBytes)}.\n\n" +
                "This cannot be undone. Continue?"))
        {
            StatusText = "Cleaning cancelled.";
            return;
        }

        IsBusy = true;
        _cancellation = new CancellationTokenSource();
        var options = new CleanOptions { PreviewOnly = preview, WriteLog = _shell.WriteLog };
        var progress = new Progress<string>(path => CurrentPath = path);
        var reports = new List<CleanReport>();

        try
        {
            foreach (var module in modules)
            {
                StatusText = preview ? $"Simulating {module.Name}..." : $"Cleaning {module.Name}...";
                var report = await module.Module
                    .CleanAsync(module.Result!, options, progress, _cancellation.Token)
                    .ConfigureAwait(true);
                reports.Add(report);
            }

            _shell.PublishReports(reports, preview);

            var freed = reports.Sum(report => report.BytesFreed);
            StatusText = preview
                ? $"Simulation finished: {SizeFormatter.Format(freed)} would be freed. Nothing was deleted."
                : $"Cleaning finished: {SizeFormatter.Format(freed)} freed.";

            if (!preview)
            {
                // The findings are stale once the files are gone.
                foreach (var module in Modules)
                {
                    module.Reset();
                }

                HasResults = false;
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cleaning cancelled.";
        }
        finally
        {
            CurrentPath = string.Empty;
            IsBusy = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private void Cancel()
    {
        _cancellation?.Cancel();
        StatusText = "Cancelling...";
    }

    private void SetAllSelected(bool selected)
    {
        foreach (var module in Modules)
        {
            module.IsSelected = selected;
        }
    }

    private long TotalSelectedBytes => Modules.Sum(module => module.SelectedBytes);

    private int TotalSelectedItems => Modules.Sum(module => module.SelectedItems);

    private void OnSelectionChanged()
    {
        _shell.StoreModuleSelection(Modules);
        OnResultChanged();
        OnPropertyChanged(nameof(SelectionSummary));
    }

    /// <summary>A finished scan changes what can be cleaned, but not what the user picked.</summary>
    private void OnResultChanged()
    {
        HasResults = Modules.Any(module => module.IsSelected && module.HasFindings);
        RefreshCommands();
    }

    public string SelectionSummary =>
        $"{Modules.Count(module => module.IsSelected)} of {Modules.Count} selected";

    private void RefreshCommands()
    {
        AnalyzeCommand.RaiseCanExecuteChanged();
        CleanCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }
}
