using CleaN.Core;

namespace CleaN.Modules;

/// <summary>
/// Cleans %TEMP% and C:\Windows\Temp.
///
/// Items touched in the last <see cref="MinimumAgeHours"/> hours are left alone: installers
/// and running applications keep live state there, and deleting it mid-install breaks things.
/// </summary>
public sealed class TempFilesModule : CleanerModuleBase
{
    public const int MinimumAgeHours = 24;

    public override string Id => "temp-files";

    public override string Name => "Temporary files";

    public override string Description =>
        $"Contents of %TEMP% and C:\\Windows\\Temp that have not been touched for {MinimumAgeHours} hours.";

    public override bool RequiresElevation => true;

    protected override IReadOnlyList<string> BuildAllowedRoots()
    {
        var roots = new List<string>();
        AddIfPresent(roots, Path.GetTempPath());
        AddIfPresent(roots, Environment.GetEnvironmentVariable("TMP"));
        AddIfPresent(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
        return roots;
    }

    protected override ScanResult Scan(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var targets = new List<CleanTarget>();
        var cutoff = DateTime.Now.AddHours(-MinimumAgeHours);

        foreach (var root in AllowedRoots)
        {
            foreach (var target in FileSystemProbe.ChildrenAsTargets(root, warnings, root, cancellationToken))
            {
                if (LastTouched(target.Path) <= cutoff)
                {
                    targets.Add(target);
                }
            }
        }

        return new ScanResult(Id, targets, warnings);
    }

    private static DateTime LastTouched(string path)
    {
        try
        {
            var written = Directory.Exists(path)
                ? Directory.GetLastWriteTime(path)
                : File.GetLastWriteTime(path);
            var accessed = Directory.Exists(path)
                ? Directory.GetLastAccessTime(path)
                : File.GetLastAccessTime(path);
            return written > accessed ? written : accessed;
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            // Unknown age: treat it as brand new so it is skipped rather than deleted.
            return DateTime.Now;
        }
    }
}
