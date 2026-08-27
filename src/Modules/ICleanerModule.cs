using CleaN.Core;

namespace CleaN.Modules;

/// <summary>Which tab a module belongs to in the UI.</summary>
public enum CleanCategory
{
    SystemJunk,
    BrowserData,
    RecycleBin,
}

/// <summary>
/// A unit of cleaning. Modules only ever *describe* what they would remove; deletion is
/// centralised in <see cref="FileSweeper"/> so the safety rules cannot be bypassed.
/// </summary>
public interface ICleanerModule
{
    /// <summary>Stable identifier used in settings and logs.</summary>
    string Id { get; }

    string Name { get; }

    string Description { get; }

    CleanCategory Category { get; }

    /// <summary>
    /// False for anything that destroys user-visible state (cookies, history, saved
    /// sessions). Those must always be an explicit, deliberate choice.
    /// </summary>
    bool EnabledByDefault { get; }

    bool RequiresElevation { get; }

    /// <summary>Locations this module is allowed to delete inside. Enforced by the sweeper.</summary>
    IReadOnlyList<string> AllowedRoots { get; }

    Task<ScanResult> ScanAsync(CancellationToken cancellationToken);

    Task<CleanReport> CleanAsync(ScanResult scan, CleanOptions options, IProgress<string>? progress,
        CancellationToken cancellationToken);
}
