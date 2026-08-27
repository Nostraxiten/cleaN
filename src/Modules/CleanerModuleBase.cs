using CleaN.Core;

namespace CleaN.Modules;

/// <summary>Shared plumbing: scanning runs off the UI thread, cleaning goes through the sweeper.</summary>
public abstract class CleanerModuleBase : ICleanerModule
{
    public abstract string Id { get; }

    public abstract string Name { get; }

    public abstract string Description { get; }

    public virtual CleanCategory Category => CleanCategory.SystemJunk;

    public virtual bool EnabledByDefault => true;

    public virtual bool RequiresElevation => false;

    public IReadOnlyList<string> AllowedRoots => _allowedRoots ??= BuildAllowedRoots();

    private IReadOnlyList<string>? _allowedRoots;

    /// <summary>Locations this module may delete inside. Keep them as narrow as possible.</summary>
    protected abstract IReadOnlyList<string> BuildAllowedRoots();

    /// <summary>Runs on a background thread; must not touch the UI.</summary>
    protected abstract ScanResult Scan(CancellationToken cancellationToken);

    public Task<ScanResult> ScanAsync(CancellationToken cancellationToken) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    public virtual Task<CleanReport> CleanAsync(ScanResult scan, CleanOptions options, IProgress<string>? progress,
        CancellationToken cancellationToken) =>
        Task.Run(() => FileSweeper.Sweep(Id, scan.Targets, AllowedRoots, options, progress, cancellationToken),
            cancellationToken);

    /// <summary>Expands an environment path and returns an empty string when it does not exist.</summary>
    protected static string ExistingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path);
            return Directory.Exists(expanded) ? SafetyGuard.Normalize(expanded) : string.Empty;
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            return string.Empty;
        }
    }

    protected static void AddIfPresent(ICollection<string> roots, string? path)
    {
        var directory = ExistingDirectory(path);
        if (directory.Length > 0 && !roots.Contains(directory))
        {
            roots.Add(directory);
        }
    }
}
