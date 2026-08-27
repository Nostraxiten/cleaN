using System.Collections.ObjectModel;
using System.Globalization;
using CleaN.Core;

namespace CleaN.ViewModels;

public sealed class LogFileItemViewModel
{
    public LogFileItemViewModel(FileInfo file)
    {
        File = file;
    }

    public FileInfo File { get; }

    public string Name => File.Name;

    public string DateText => File.LastWriteTime.ToString("g", CultureInfo.CurrentCulture);

    public string SizeText => SizeFormatter.Format(File.Length);
}

/// <summary>
/// The audit trail: what the last run did, plus every log cleaN has written so far.
/// </summary>
public sealed class ReportViewModel : ObservableObject
{
    private readonly MainViewModel _shell;
    private LogFileItemViewModel? _selectedLog;
    private string _logContent = string.Empty;
    private string _lastRunText = "No cleaning has been run in this session yet.";

    public ReportViewModel(MainViewModel shell)
    {
        _shell = shell;
        RefreshCommand = new RelayCommand(Refresh);
        OpenFolderCommand = new RelayCommand(() => _shell.Dialogs.OpenInExplorer(CleanLogger.LogDirectory));
        Refresh();
    }

    public ObservableCollection<LogFileItemViewModel> Logs { get; } = new();

    public RelayCommand RefreshCommand { get; }

    public RelayCommand OpenFolderCommand { get; }

    public string LogDirectory => CleanLogger.LogDirectory;

    public string LastRunText
    {
        get => _lastRunText;
        private set => SetProperty(ref _lastRunText, value);
    }

    public LogFileItemViewModel? SelectedLog
    {
        get => _selectedLog;
        set
        {
            if (SetProperty(ref _selectedLog, value))
            {
                LoadContent();
            }
        }
    }

    public string LogContent
    {
        get => _logContent;
        private set => SetProperty(ref _logContent, value);
    }

    public bool HasLogs => Logs.Count > 0;

    /// <summary>Records the outcome of a run and, unless disabled, writes it to disk.</summary>
    public void Publish(IReadOnlyList<CleanReport> reports, bool previewOnly)
    {
        var items = reports.Sum(report => report.Deleted.Count);
        var bytes = reports.Sum(report => report.BytesFreed);
        var failed = reports.Sum(report => report.Failed.Count);
        var protectedItems = reports.Sum(report => report.Skipped.Count);

        var summary = previewOnly
            ? $"Simulation at {DateTime.Now:g}: {items:N0} item(s), {SizeFormatter.Format(bytes)} would be freed."
            : $"Cleaning at {DateTime.Now:g}: {items:N0} item(s) deleted, {SizeFormatter.Format(bytes)} freed.";

        if (failed > 0)
        {
            summary += $" {failed:N0} item(s) could not be deleted (usually in use).";
        }

        if (protectedItems > 0)
        {
            summary += $" {protectedItems:N0} item(s) were protected and left untouched.";
        }

        LastRunText = summary;
        LogContent = CleanLogger.Render(reports, previewOnly);

        if (_shell.WriteLog)
        {
            CleanLogger.Write(reports, previewOnly);
            Refresh();
        }
    }

    private void Refresh()
    {
        Logs.Clear();
        foreach (var file in CleanLogger.RecentLogs())
        {
            Logs.Add(new LogFileItemViewModel(file));
        }

        OnPropertyChanged(nameof(HasLogs));
    }

    private void LoadContent()
    {
        if (SelectedLog is null)
        {
            return;
        }

        try
        {
            LogContent = File.ReadAllText(SelectedLog.File.FullName);
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            LogContent = $"The log could not be read: {FileSystemProbe.Describe(ex)}";
        }
    }
}
