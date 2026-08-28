using CleaN.Core;

namespace CleaN.Modules;

/// <summary>
/// Removes old diagnostic logs from C:\Windows\Logs and leftovers in
/// C:\Windows\Downloaded Program Files. Only files older than <see cref="MinimumAgeDays"/>
/// are proposed, so recent troubleshooting data survives.
/// </summary>
public sealed class SystemLogsModule : CleanerModuleBase
{
    public const int MinimumAgeDays = 7;

    public override string Id => "system-logs";

    public override string Name => "Old system logs";

    public override string Description =>
        $"Diagnostic logs in C:\\Windows\\Logs older than {MinimumAgeDays} days, plus stale downloaded program files.";

    public override bool RequiresElevation => true;

    protected override IReadOnlyList<string> BuildAllowedRoots()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var roots = new List<string>();
        AddIfPresent(roots, Path.Combine(windows, "Logs"));
        AddIfPresent(roots, Path.Combine(windows, "Downloaded Program Files"));
        return roots;
    }

    protected override ScanResult Scan(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var targets = new List<CleanTarget>();
        var cutoff = DateTime.Now.AddDays(-MinimumAgeDays);

        foreach (var root in AllowedRoots)
        {
            foreach (var file in FileSystemProbe.EnumerateFiles(root, warnings, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (file.LastWriteTime > cutoff)
                {
                    continue;
                }

                targets.Add(new CleanTarget(file.FullName, false, SafeLength(file), Path.GetFileName(root)));
            }
        }

        return new ScanResult(Id, targets, warnings);
    }

    private static long SafeLength(FileInfo file)
    {
        try
        {
            return file.Length;
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            return 0;
        }
    }
}
