using CleaN.Core;

namespace CleaN.Modules;

/// <summary>
/// Removes the installers Windows Update already applied, from
/// C:\Windows\SoftwareDistribution\Download. Windows re-downloads anything it still needs.
/// </summary>
public sealed class WindowsUpdateCacheModule : CleanerModuleBase
{
    public override string Id => "windows-update-cache";

    public override string Name => "Windows Update cache";

    public override string Description =>
        "Update packages already installed, kept in C:\\Windows\\SoftwareDistribution\\Download.";

    public override bool RequiresElevation => true;

    protected override IReadOnlyList<string> BuildAllowedRoots()
    {
        var roots = new List<string>();
        AddIfPresent(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download"));
        return roots;
    }

    protected override ScanResult Scan(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var targets = new List<CleanTarget>();

        foreach (var root in AllowedRoots)
        {
            targets.AddRange(FileSystemProbe.ChildrenAsTargets(root, warnings, "Windows Update", cancellationToken));
        }

        if (AllowedRoots.Count == 0)
        {
            warnings.Add("The Windows Update download cache was not found.");
        }

        return new ScanResult(Id, targets, warnings);
    }
}
