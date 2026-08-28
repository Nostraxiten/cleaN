using CleaN.Core;

namespace CleaN.Modules;

/// <summary>
/// Deletes the Explorer thumbnail and icon cache databases. Windows rebuilds them on demand;
/// the files are usually locked while Explorer is running, which is reported as a failure
/// rather than treated as an error.
/// </summary>
public sealed class ThumbnailCacheModule : CleanerModuleBase
{
    public override string Id => "thumbnail-cache";

    public override string Name => "Thumbnail cache";

    public override string Description =>
        "Explorer thumbnail and icon cache databases. Windows regenerates them automatically.";

    protected override IReadOnlyList<string> BuildAllowedRoots()
    {
        var roots = new List<string>();
        AddIfPresent(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Explorer"));
        return roots;
    }

    protected override ScanResult Scan(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var targets = new List<CleanTarget>();

        foreach (var root in AllowedRoots)
        {
            foreach (var pattern in new[] { "thumbcache_*.db", "iconcache_*.db" })
            {
                targets.AddRange(FileSystemProbe.FilesAsTargets(root, pattern, false, warnings,
                    "Explorer cache", cancellationToken));
            }
        }

        return new ScanResult(Id, targets, warnings);
    }
}
