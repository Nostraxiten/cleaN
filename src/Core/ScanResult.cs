namespace CleaN.Core;

/// <summary>Everything a single module found during a scan.</summary>
public sealed class ScanResult
{
    public ScanResult(string moduleId, IReadOnlyList<CleanTarget> targets, IReadOnlyList<string>? warnings = null)
    {
        ModuleId = moduleId;
        Targets = targets;
        Warnings = warnings ?? Array.Empty<string>();
    }

    public string ModuleId { get; }

    public IReadOnlyList<CleanTarget> Targets { get; }

    /// <summary>Non-fatal problems, typically "access denied" on a path that needs elevation.</summary>
    public IReadOnlyList<string> Warnings { get; }

    public int ItemCount => Targets.Count;

    public long TotalBytes
    {
        get
        {
            long total = 0;
            foreach (var target in Targets)
            {
                total += target.SizeBytes;
            }

            return total;
        }
    }

    public static ScanResult Empty(string moduleId) => new(moduleId, Array.Empty<CleanTarget>());
}
