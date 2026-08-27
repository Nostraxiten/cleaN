using CleaN.Core;

namespace CleaN.Modules;

/// <summary>
/// Finds folders that contain no files at any depth. A folder whose only content is other
/// empty folders counts as empty, so whole abandoned trees are collapsed in one pass.
///
/// Results are always presented for confirmation before anything is removed, and every
/// candidate still has to clear <see cref="SafetyGuard"/> at deletion time.
/// </summary>
public sealed class EmptyFolderScanner
{
    /// <summary>Guards against pathological trees and against stack overflow.</summary>
    public const int MaximumDepth = 48;

    public string Id => "empty-folders";

    /// <summary>The user profile, which is where abandoned folders actually accumulate.</summary>
    public static IReadOnlyList<string> DefaultRoots()
    {
        var roots = new List<string>();
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Directory.Exists(profile))
        {
            roots.Add(SafetyGuard.Normalize(profile));
        }

        return roots;
    }

    /// <summary>Fixed drives the user can add to the scan.</summary>
    public static IReadOnlyList<string> AvailableDrives()
    {
        var drives = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    drives.Add(drive.Name);
                }
            }
        }
        catch (IOException)
        {
            // No drive information available; the default roots still work.
        }

        return drives;
    }

    public Task<ScanResult> ScanAsync(IReadOnlyList<string> roots, IProgress<string>? progress,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        var warnings = new List<string>();
        var found = new List<CleanTarget>();
        var effectiveRoots = roots.Count > 0 ? roots : DefaultRoots();

        foreach (var root in effectiveRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(root))
            {
                warnings.Add($"{root}: the folder does not exist.");
                continue;
            }

            Visit(root, effectiveRoots, found, warnings, progress, 0, cancellationToken);
        }

        // Deepest first, so deleting a parent never fights with a child still on the list.
        found.Sort((left, right) => right.Path.Length.CompareTo(left.Path.Length));
        return new ScanResult(Id, found, warnings);
    }, cancellationToken);

    public Task<CleanReport> DeleteAsync(IEnumerable<CleanTarget> targets, IReadOnlyList<string> roots,
        CleanOptions options, IProgress<string>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => FileSweeper.Sweep(Id, targets, roots.Count > 0 ? roots : DefaultRoots(), options, progress,
            cancellationToken), cancellationToken);

    /// <summary>Returns true when <paramref name="directory"/> holds no files at any depth.</summary>
    private bool Visit(string directory, IReadOnlyList<string> roots, List<CleanTarget> found, List<string> warnings,
        IProgress<string>? progress, int depth, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (depth >= MaximumDepth)
        {
            return false;
        }

        if (depth <= 2)
        {
            progress?.Report(directory);
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(directory);
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            warnings.Add($"{directory}: {FileSystemProbe.Describe(ex)}");
            return false;
        }

        var empty = files.Length == 0;

        foreach (var child in FileSystemProbe.SafeSubdirectories(directory, warnings))
        {
            if (!ShouldDescend(child))
            {
                empty = false;
                continue;
            }

            if (!Visit(child, roots, found, warnings, progress, depth + 1, cancellationToken))
            {
                empty = false;
            }
        }

        if (!empty || depth == 0)
        {
            // The root of the scan is never proposed for deletion, even when it is empty.
            return empty;
        }

        if (SafetyGuard.IsSafeToDelete(directory, roots, out _))
        {
            found.Add(new CleanTarget(directory, true, 0, "empty folder"));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Keeps the scan out of system areas, cloud-synced folders and anything behind a
    /// junction. These are excluded for speed as much as for safety.
    /// </summary>
    private static bool ShouldDescend(string directory)
    {
        if (SafetyGuard.IsReparsePoint(directory))
        {
            return false;
        }

        try
        {
            var attributes = File.GetAttributes(directory);
            if ((attributes & FileAttributes.System) == FileAttributes.System)
            {
                return false;
            }
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            return false;
        }

        // If nothing inside could ever be deleted, there is no point walking it.
        return SafetyGuard.IsSafeToDelete(Path.Combine(directory, "probe", "probe"), new[] { directory }, out _);
    }
}
