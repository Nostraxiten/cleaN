using System.Collections.ObjectModel;
using CleaN.Core;
using CleaN.Modules;

namespace CleaN.ViewModels;

/// <summary>
/// Finds folders with no files inside and lists them for confirmation. Nothing here is
/// ever removed without the user ticking it and pressing the button.
/// </summary>
public sealed class EmptyFoldersViewModel : ObservableObject
{
    private readonly MainViewModel _shell;
    private readonly EmptyFolderScanner _scanner = new();
    private CancellationTokenSource? _cancellation;
    private string _statusText = "Choose where to look, then press Scan.";
    private string _currentPath = string.Empty;
    private string _customRoot = string.Empty;
    private bool _isBusy;

    public EmptyFoldersViewModel(MainViewModel shell)
    {
        _shell = shell;

        var stored = shell.Settings.EmptyFolderRoots;
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Directory.Exists(profile))
        {
            Roots.Add(new ScanRootViewModel(profile, $"Your user folder ({profile})",
                stored.Count == 0 || stored.Contains(profile, StringComparer.OrdinalIgnoreCase), OnRootsChanged));
        }

        foreach (var drive in EmptyFolderScanner.AvailableDrives())
        {
            Roots.Add(new ScanRootViewModel(drive, $"Drive {drive}",
                stored.Contains(drive, StringComparer.OrdinalIgnoreCase), OnRootsChanged));
        }

        foreach (var custom in stored)
        {
            if (!Roots.Any(root => string.Equals(root.Path, custom, StringComparison.OrdinalIgnoreCase)))
            {
                Roots.Add(new ScanRootViewModel(custom, custom, true, OnRootsChanged));
            }
        }

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy && SelectedRoots.Count > 0);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !IsBusy && Found.Any(item => item.IsSelected));
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        SelectAllCommand = new RelayCommand(() => SetAllSelected(true));
        SelectNoneCommand = new RelayCommand(() => SetAllSelected(false));
        AddRootCommand = new RelayCommand(AddCustomRoot, () => CustomRoot.Trim().Length > 0);
    }

    public ObservableCollection<ScanRootViewModel> Roots { get; } = new();

    public ObservableCollection<TargetItemViewModel> Found { get; } = new();

    public AsyncRelayCommand ScanCommand { get; }

    public AsyncRelayCommand DeleteCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand SelectAllCommand { get; }

    public RelayCommand SelectNoneCommand { get; }

    public RelayCommand AddRootCommand { get; }

    public string CustomRoot
    {
        get => _customRoot;
        set
        {
            if (SetProperty(ref _customRoot, value))
            {
                AddRootCommand.RaiseCanExecuteChanged();
            }
        }
    }

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

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string CurrentPath
    {
        get => _currentPath;
        private set => SetProperty(ref _currentPath, value);
    }

    public string DeleteButtonText => _shell.PreviewOnly ? "Simulate deletion" : "Delete selected folders";

    public string ModeHint => _shell.PreviewOnly
        ? "Preview mode is on, so this only simulates. Untick \u201cPreview mode\u201d at the top right to delete for real."
        : "Preview mode is off: the selected folders will be deleted permanently.";

    public bool HasResults => Found.Count > 0;

    public void OnPreviewModeChanged()
    {
        OnPropertyChanged(nameof(DeleteButtonText));
        OnPropertyChanged(nameof(ModeHint));
    }

    private List<string> SelectedRoots =>
        Roots.Where(root => root.IsSelected).Select(root => root.Path).ToList();

    private async Task ScanAsync()
    {
        IsBusy = true;
        Found.Clear();
        OnPropertyChanged(nameof(HasResults));
        _cancellation = new CancellationTokenSource();
        var roots = SelectedRoots;
        var progress = new Progress<string>(path => CurrentPath = path);

        try
        {
            StatusText = "Scanning for empty folders...";
            var result = await _scanner.ScanAsync(roots, progress, _cancellation.Token).ConfigureAwait(true);

            foreach (var target in result.Targets)
            {
                Found.Add(new TargetItemViewModel(target, OnItemSelectionChanged));
            }

            StatusText = result.Targets.Count == 0
                ? "No empty folders were found in the selected locations."
                : $"{result.Targets.Count:N0} empty folder(s) found. Review the list before deleting.";

            if (result.Warnings.Count > 0)
            {
                StatusText += $" ({result.Warnings.Count} location(s) could not be read.)";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled.";
        }
        finally
        {
            CurrentPath = string.Empty;
            OnPropertyChanged(nameof(HasResults));
            IsBusy = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private async Task DeleteAsync()
    {
        var selected = Found.Where(item => item.IsSelected).Select(item => item.Target).ToList();
        if (selected.Count == 0)
        {
            StatusText = "No folder is selected.";
            return;
        }

        var preview = _shell.PreviewOnly;
        if (!preview && !_shell.Dialogs.Confirm("Delete empty folders?",
                $"cleaN will delete {selected.Count:N0} empty folder(s).\n\nContinue?"))
        {
            StatusText = "Deletion cancelled.";
            return;
        }

        IsBusy = true;
        _cancellation = new CancellationTokenSource();
        var progress = new Progress<string>(path => CurrentPath = path);

        try
        {
            var report = await _scanner.DeleteAsync(selected, SelectedRoots,
                new CleanOptions { PreviewOnly = preview, WriteLog = _shell.WriteLog }, progress,
                _cancellation.Token).ConfigureAwait(true);

            _shell.PublishReports(new[] { report }, preview);
            StatusText = preview
                ? $"Simulation finished: {report.Deleted.Count:N0} folder(s) would be removed. Nothing was deleted."
                : $"Done: {report.Deleted.Count:N0} folder(s) removed.";

            if (!preview)
            {
                foreach (var item in Found.Where(item => item.IsSelected).ToList())
                {
                    Found.Remove(item);
                }

                OnPropertyChanged(nameof(HasResults));
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Deletion cancelled.";
        }
        finally
        {
            CurrentPath = string.Empty;
            IsBusy = false;
            _cancellation?.Dispose();
            _cancellation = null;
            RefreshCommands();
        }
    }

    private void AddCustomRoot()
    {
        var path = CustomRoot.Trim().Trim('"');
        if (!Directory.Exists(path))
        {
            _shell.Dialogs.Alert("Folder not found", $"'{path}' is not an existing folder.");
            return;
        }

        var normalized = SafetyGuard.Normalize(path);
        if (Roots.Any(root => string.Equals(root.Path, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = "That location is already in the list.";
            return;
        }

        Roots.Add(new ScanRootViewModel(normalized, normalized, true, OnRootsChanged));
        CustomRoot = string.Empty;
        OnRootsChanged();
    }

    private void Cancel()
    {
        _cancellation?.Cancel();
        StatusText = "Cancelling...";
    }

    private void SetAllSelected(bool selected)
    {
        foreach (var item in Found)
        {
            item.IsSelected = selected;
        }
    }

    private void OnItemSelectionChanged() => DeleteCommand.RaiseCanExecuteChanged();

    private void OnRootsChanged()
    {
        _shell.Settings.EmptyFolderRoots = SelectedRoots;
        _shell.Settings.Save();
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        ScanCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }
}
