using System.Collections.ObjectModel;
using CleaN.Apps;

namespace CleaN.ViewModels;

/// <summary>
/// Lists installed applications ordered by how long they have gone unused.
///
/// cleaN never uninstalls anything by itself: the most it does is hand the application's
/// own uninstaller to Windows when the user asks for it.
/// </summary>
public sealed class UnusedAppsViewModel : ObservableObject
{
    private readonly MainViewModel _shell;
    private readonly UsageAnalyzer _analyzer = new();
    private readonly List<AppUsageItemViewModel> _all = new();
    private CancellationTokenSource? _cancellation;
    private string _statusText = "Press Analyze to list your installed applications by last use.";
    private string _searchText = string.Empty;
    private bool _isBusy;
    private int _thresholdDays;

    public UnusedAppsViewModel(MainViewModel shell)
    {
        _shell = shell;
        _thresholdDays = shell.Settings.UnusedAppThresholdDays;

        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        UninstallCommand = new RelayCommand(parameter => Uninstall(parameter as AppUsageItemViewModel));
        OpenLocationCommand = new RelayCommand(parameter => OpenLocation(parameter as AppUsageItemViewModel));
    }

    public ObservableCollection<AppUsageItemViewModel> Applications { get; } = new();

    public ObservableCollection<string> Warnings { get; } = new();

    public AsyncRelayCommand AnalyzeCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand UninstallCommand { get; }

    public RelayCommand OpenLocationCommand { get; }

    public bool HasWarnings => Warnings.Count > 0;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                AnalyzeCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    /// <summary>Zero means "show everything, regardless of last use".</summary>
    public int ThresholdDays
    {
        get => _thresholdDays;
        set
        {
            if (SetProperty(ref _thresholdDays, value))
            {
                _shell.Settings.UnusedAppThresholdDays = value;
                _shell.Settings.Save();
                OnPropertyChanged(nameof(IsThreshold90));
                OnPropertyChanged(nameof(IsThreshold180));
                OnPropertyChanged(nameof(IsThreshold365));
                OnPropertyChanged(nameof(IsThresholdAll));
                ApplyFilter();
            }
        }
    }

    // Bound to the filter chips; setting one clears the others through ThresholdDays.
    public bool IsThreshold90
    {
        get => ThresholdDays == 90;
        set { if (value) { ThresholdDays = 90; } }
    }

    public bool IsThreshold180
    {
        get => ThresholdDays == 180;
        set { if (value) { ThresholdDays = 180; } }
    }

    public bool IsThreshold365
    {
        get => ThresholdDays == 365;
        set { if (value) { ThresholdDays = 365; } }
    }

    public bool IsThresholdAll
    {
        get => ThresholdDays == 0;
        set { if (value) { ThresholdDays = 0; } }
    }

    private async Task AnalyzeAsync()
    {
        IsBusy = true;
        _cancellation = new CancellationTokenSource();
        StatusText = "Reading installed applications and their launch history...";
        Warnings.Clear();
        OnPropertyChanged(nameof(HasWarnings));

        try
        {
            var result = await _analyzer.AnalyzeAsync(_cancellation.Token).ConfigureAwait(true);

            _all.Clear();
            foreach (var app in result.Applications)
            {
                _all.Add(new AppUsageItemViewModel(app));
            }

            foreach (var warning in result.Warnings)
            {
                Warnings.Add(warning);
            }

            OnPropertyChanged(nameof(HasWarnings));
            ApplyFilter();

            var unused = _all.Count(item => ThresholdDays == 0 || item.Info.IsUnused(ThresholdDays));
            StatusText = $"{_all.Count:N0} application(s) installed. " +
                         (ThresholdDays == 0
                             ? "Showing all of them, oldest use first."
                             : $"{unused:N0} have not been used in {ThresholdDays} days or more.");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analysis cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"The applications could not be read: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cancellation?.Dispose();
            _cancellation = null;
        }
    }

    private void ApplyFilter()
    {
        Applications.Clear();
        var search = SearchText.Trim();

        foreach (var item in _all)
        {
            if (ThresholdDays > 0 && !item.Info.IsUnused(ThresholdDays))
            {
                continue;
            }

            if (search.Length > 0 &&
                item.Name.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) < 0 &&
                item.Publisher.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) < 0)
            {
                continue;
            }

            Applications.Add(item);
        }

        OnPropertyChanged(nameof(HasApplications));
    }

    public bool HasApplications => Applications.Count > 0;

    private void Uninstall(AppUsageItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (!_shell.Dialogs.Confirm("Uninstall this application?",
                $"cleaN will start the uninstaller for:\n\n{item.Name}\n\n" +
                "The application's own uninstaller takes over from here. Continue?"))
        {
            return;
        }

        if (AppUninstaller.TryLaunch(item.Info.App, out var error))
        {
            StatusText = $"The uninstaller for {item.Name} was started.";
            return;
        }

        _shell.Dialogs.Alert("The uninstaller could not be started", error);
    }

    private void OpenLocation(AppUsageItemViewModel? item)
    {
        if (item is { HasInstallLocation: true })
        {
            _shell.Dialogs.OpenInExplorer(item.InstallLocation);
        }
    }

    private void Cancel()
    {
        _cancellation?.Cancel();
        StatusText = "Cancelling...";
    }
}
