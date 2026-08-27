namespace CleaN.Core;

/// <summary>
/// Defensive file system helpers. Every method swallows the usual "the file vanished /
/// is locked / needs elevation" errors and reports them as warnings instead of throwing,
/// because a cleaner that dies halfway through a scan is worse than useless.
/// </summary>
public static class FileSystemProbe
{
    /// <summary>Recursive size of a directory in bytes; unreadable branches count as zero.</summary>
    public static long DirectorySize(string path, CancellationToken cancellationToken = default)
    {
        long total = 0;
        foreach (var file in EnumerateFiles(path, null, cancellationToken))
        {
            total += file.Length;
        }

        return total;
    }

    public static long FileSize(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : 0;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return 0;
        }
    }

    /// <summary>
    /// Depth-first file enumeration that never follows junctions or symbolic links and
    /// never throws on an unreadable directory.
    /// </summary>
    public static IEnumerable<FileInfo> EnumerateFiles(string root, ICollection<string>? warnings = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();

            FileInfo[] files;
            try
            {
                files = new DirectoryInfo(current).GetFiles();
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                warnings?.Add($"{current}: {Describe(ex)}");
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            foreach (var directory in SafeSubdirectories(current, warnings))
            {
                pending.Push(directory);
            }
        }
    }

    /// <summary>Subdirectories of <paramref name="path"/>, skipping reparse points.</summary>
    public static IReadOnlyList<string> SafeSubdirectories(string path, ICollection<string>? warnings = null)
    {
        try
        {
            var result = new List<string>();
            foreach (var directory in Directory.GetDirectories(path))
            {
                if (!SafetyGuard.IsReparsePoint(directory))
                {
                    result.Add(directory);
                }
            }

            return result;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            warnings?.Add($"{path}: {Describe(ex)}");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Direct children of <paramref name="root"/> as clean targets, sized recursively.
    /// Modules use this to expose a per-item preview instead of one opaque folder entry.
    /// </summary>
    public static IReadOnlyList<CleanTarget> ChildrenAsTargets(string root, ICollection<string>? warnings = null,
        string? note = null, CancellationToken cancellationToken = default)
    {
        var targets = new List<CleanTarget>();
        if (!Directory.Exists(root))
        {
            return targets;
        }

        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(root);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            warnings?.Add($"{root}: {Describe(ex)}");
            return targets;
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (SafetyGuard.IsReparsePoint(entry))
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                targets.Add(new CleanTarget(entry, true, DirectorySize(entry, cancellationToken), note));
            }
            else if (File.Exists(entry))
            {
                targets.Add(new CleanTarget(entry, false, FileSize(entry), note));
            }
        }

        return targets;
    }

    /// <summary>Files under <paramref name="root"/> matching a pattern, as clean targets.</summary>
    public static IReadOnlyList<CleanTarget> FilesAsTargets(string root, string searchPattern, bool recursive,
        ICollection<string>? warnings = null, string? note = null, CancellationToken cancellationToken = default)
    {
        var targets = new List<CleanTarget>();
        if (!Directory.Exists(root))
        {
            return targets;
        }

        try
        {
            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var file in Directory.EnumerateFiles(root, searchPattern, option))
            {
                cancellationToken.ThrowIfCancellationRequested();
                targets.Add(new CleanTarget(file, false, FileSize(file), note));
            }
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            warnings?.Add($"{root}: {Describe(ex)}");
        }

        return targets;
    }

    /// <summary>True when the exception is one of the everyday file system failures.</summary>
    public static bool IsExpected(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException
            or System.Security.SecurityException;

    public static string Describe(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "access denied (try running cleaN as administrator)",
        DirectoryNotFoundException => "the folder no longer exists",
        FileNotFoundException => "the file no longer exists",
        PathTooLongException => "the path is too long",
        IOException io => io.Message,
        _ => ex.Message,
    };
}
