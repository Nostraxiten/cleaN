using CleaN.Core;

namespace CleaN.Modules;

/// <summary>
/// Shared scanning logic for the browser modules. Each concrete module only declares which
/// files or folders inside a profile it owns; discovery, warnings and deletion are shared.
/// </summary>
public abstract class BrowserDataModuleBase : CleanerModuleBase
{
    public override CleanCategory Category => CleanCategory.BrowserData;

    protected override IReadOnlyList<string> BuildAllowedRoots() => BrowserCatalog.DiscoverRoots();

    /// <summary>Adds this module's targets for a single browser profile.</summary>
    protected abstract void CollectTargets(BrowserProfile profile, List<CleanTarget> targets,
        List<string> warnings, CancellationToken cancellationToken);

    protected override ScanResult Scan(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var targets = new List<CleanTarget>();
        var profiles = BrowserCatalog.Discover();

        if (profiles.Count == 0)
        {
            warnings.Add("No supported browser profile was found.");
            return new ScanResult(Id, targets, warnings);
        }

        foreach (var browser in BrowserCatalog.RunningBrowsers(profiles))
        {
            warnings.Add($"{browser} is running: close it first or some files will stay locked.");
        }

        foreach (var profile in profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CollectTargets(profile, targets, warnings, cancellationToken);
        }

        return new ScanResult(Id, targets, warnings);
    }

    /// <summary>Adds the *contents* of a folder inside the profile, keeping the folder itself.</summary>
    protected static void AddFolderContents(BrowserProfile profile, string root, string relative,
        List<CleanTarget> targets, List<string> warnings, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(root, relative);
        if (Directory.Exists(directory))
        {
            targets.AddRange(FileSystemProbe.ChildrenAsTargets(directory, warnings, profile.DisplayName,
                cancellationToken));
        }
    }

    /// <summary>Adds specific files inside the profile when they exist.</summary>
    protected static void AddFiles(BrowserProfile profile, string root, IEnumerable<string> relativeFiles,
        List<CleanTarget> targets)
    {
        foreach (var relative in relativeFiles)
        {
            var file = Path.Combine(root, relative);
            if (File.Exists(file))
            {
                targets.Add(new CleanTarget(file, false, FileSystemProbe.FileSize(file), profile.DisplayName));
            }
        }
    }
}
