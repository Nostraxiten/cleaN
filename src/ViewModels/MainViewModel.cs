using System.Security.Principal;
using CleaN.Core;
using CleaN.Modules;

namespace CleaN.ViewModels;

/// <summary>Applies a theme to the running application.</summary>
public interface IThemeService
{
    void Apply(AppTheme theme);
}

/// <summary>The shell: shared state, the six sections, and the theme switch.</summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly IThemeService _themes;

    public MainViewModel(IDialogService dialogs, IThemeService themes, AppSettings settings)
    {
        Dialogs = dialogs;
        _themes = themes;
        Settings = settings;

        SystemJunk = new CleanupViewModel(this, "Temporary files",
            "Junk Windows and your applications leave behind. Everything here is safe to remove.",
            new ICleanerModule[]
            {
                new TempFilesModule(),
                new WindowsUpdateCacheModule(),
                new ThumbnailCacheModule(),
                new ErrorReportsModule(),
                new SystemLogsModule(),
            });

        BrowserData = new CleanupViewModel(this, "Browser cache",
            "Detected browsers and their profiles. Cache is safe; cookies and history are opt-in because they log you out and cannot be undone.",
            new ICleanerModule[]
            {
                new BrowserCacheModule(),
                new BrowserCookiesModule(),
                new BrowserHistoryModule(),
            });

        RecycleBin = new CleanupViewModel(this, "Recycle Bin",
            "Emptying the Recycle Bin is permanent: this is your last chance to recover those files.",
            new ICleanerModule[] { new RecycleBinModule() });

        EmptyFolders = new EmptyFoldersViewModel(this);
        UnusedApps = new UnusedAppsViewModel(this);
        Report = new ReportViewModel(this);

        ToggleThemeCommand = new RelayCommand(ToggleTheme);

        CaptureInitialSelection();
    }

    public IDialogService Dialogs { get; }

    public AppSettings Settings { get; }

    public CleanupViewModel SystemJunk { get; }

    public CleanupViewModel BrowserData { get; }

    public CleanupViewModel RecycleBin { get; }

    public EmptyFoldersViewModel EmptyFolders { get; }

    public UnusedAppsViewModel UnusedApps { get; }

    public ReportViewModel Report { get; }

    public RelayCommand ToggleThemeCommand { get; }

    public string Title => "cleaN";

    public string Version => "0.1.0";

    /// <summary>
    /// Preview mode is the default and is deliberately prominent: with it on, cleaN reports
    /// what it would delete and touches nothing.
    /// </summary>
    public bool PreviewOnly
    {
        get => Settings.PreviewOnly;
        set
        {
            if (Settings.PreviewOnly == value)
            {
                return;
            }

            Settings.PreviewOnly = value;
            Settings.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModeDescription));

            SystemJunk.OnPreviewModeChanged();
            BrowserData.OnPreviewModeChanged();
            RecycleBin.OnPreviewModeChanged();
            EmptyFolders.OnPreviewModeChanged();
        }
    }

    public string ModeDescription => PreviewOnly
        ? "Preview mode: cleaN shows what it would delete without touching anything."
        : "Cleaning mode: selected items will be deleted permanently.";

    public bool WriteLog
    {
        get => Settings.WriteLog;
        set
        {
            if (Settings.WriteLog == value)
            {
                return;
            }

            Settings.WriteLog = value;
            Settings.Save();
            OnPropertyChanged();
        }
    }

    public bool IsDarkTheme
    {
        get => Settings.Theme == AppTheme.Dark;
        set
        {
            var theme = value ? AppTheme.Dark : AppTheme.Light;
            if (Settings.Theme == theme)
            {
                return;
            }

            Settings.Theme = theme;
            Settings.Save();
            _themes.Apply(theme);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeButtonText));
        }
    }

    public string ThemeButtonText => IsDarkTheme ? "Light mode" : "Dark mode";

    /// <summary>True when cleaN is running elevated; several modules need it to see everything.</summary>
    public bool IsElevated
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException
                                           or PlatformNotSupportedException)
            {
                return false;
            }
        }
    }

    public bool ShowElevationWarning => !IsElevated;

    public string ElevationWarning =>
        "cleaN is not running as administrator. System-wide locations and the Prefetch launch history " +
        "cannot be read, so some results will be incomplete.";

    public bool IsModuleSelected(ICleanerModule module) =>
        Settings.SelectedModules is { } stored ? stored.Contains(module.Id) : module.EnabledByDefault;

    /// <summary>
    /// On a first run there is no stored selection, so every module falls back to its own
    /// default. That default has to be written out for *all* modules before the user touches
    /// anything: otherwise the first checkbox they tick would save a list containing only
    /// that tab's modules, silently deselecting every other tab on the next launch.
    /// </summary>
    private void CaptureInitialSelection()
    {
        if (Settings.SelectedModules is not null)
        {
            return;
        }

        var selected = new List<string>();
        foreach (var section in new[] { SystemJunk, BrowserData, RecycleBin })
        {
            foreach (var module in section.Modules)
            {
                if (module.IsSelected)
                {
                    selected.Add(module.Id);
                }
            }
        }

        Settings.SelectedModules = selected;
        Settings.Save();
    }

    public void StoreModuleSelection(IEnumerable<ModuleViewModel> modules)
    {
        var stored = Settings.SelectedModules ??= new List<string>();

        foreach (var module in modules)
        {
            if (module.IsSelected && !stored.Contains(module.Id))
            {
                stored.Add(module.Id);
            }
            else if (!module.IsSelected)
            {
                stored.Remove(module.Id);
            }
        }

        Settings.Save();
    }

    public void PublishReports(IReadOnlyList<CleanReport> reports, bool previewOnly) =>
        Report.Publish(reports, previewOnly);

    private void ToggleTheme() => IsDarkTheme = !IsDarkTheme;
}
