using CleaN.Core;

namespace CleaN.Modules;

/// <summary>Clears Windows Error Reporting queues and archives (per-user and machine-wide).</summary>
public sealed class ErrorReportsModule : CleanerModuleBase
{
    public override string Id => "error-reports";

    public override string Name => "Windows error reports";

    public override string Description =>
        "Crash dumps and reports queued by Windows Error Reporting (WER).";

    public override bool RequiresElevation => true;

    protected override IReadOnlyList<string> BuildAllowedRoots()
    {
        var roots = new List<string>();
        AddIfPresent(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "WER"));
        AddIfPresent(roots, Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER"));
        return roots;
    }

    protected override ScanResult Scan(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var targets = new List<CleanTarget>();

        foreach (var root in AllowedRoots)
        {
            foreach (var queue in new[] { "ReportArchive", "ReportQueue", "Temp" })
            {
                var directory = Path.Combine(root, queue);
                if (Directory.Exists(directory))
                {
                    targets.AddRange(FileSystemProbe.ChildrenAsTargets(directory, warnings, queue, cancellationToken));
                }
            }
        }

        return new ScanResult(Id, targets, warnings);
    }
}
