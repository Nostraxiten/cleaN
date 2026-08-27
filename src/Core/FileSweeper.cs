namespace CleaN.Core;

/// <summary>
/// The only place in cleaN where files are actually deleted. Every target passes through
/// <see cref="SafetyGuard"/> first, and in preview mode nothing is touched at all.
/// </summary>
public static class FileSweeper
{
    public static CleanReport Sweep(string moduleId, IEnumerable<CleanTarget> targets,
        IReadOnlyList<string> allowedRoots, CleanOptions options,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var report = new CleanReport(moduleId, options.PreviewOnly);

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SafetyGuard.IsSafeToDelete(target.Path, allowedRoots, out var reason))
            {
                report.RecordSkipped(target.Path, reason);
                continue;
            }

            if (options.PreviewOnly)
            {
                report.RecordDeleted(target.Path, target.SizeBytes);
                continue;
            }

            progress?.Report(target.Path);

            if (target.IsDirectory)
            {
                var freed = DeleteDirectory(target.Path, allowedRoots, report, cancellationToken);
                if (freed > 0 || !Directory.Exists(target.Path))
                {
                    report.RecordDeleted(target.Path, freed);
                }
            }
            else if (TryDeleteFile(target.Path, out var error))
            {
                report.RecordDeleted(target.Path, target.SizeBytes);
            }
            else
            {
                report.RecordFailed(target.Path, error);
            }
        }

        return report;
    }

    /// <summary>
    /// Deletes a directory tree file by file so that a single locked file does not abort
    /// the whole operation, then removes the directories bottom-up. Returns bytes freed.
    /// </summary>
    private static long DeleteDirectory(string root, IReadOnlyList<string> allowedRoots, CleanReport report,
        CancellationToken cancellationToken)
    {
        long freed = 0;
        if (!Directory.Exists(root))
        {
            return 0;
        }

        foreach (var file in FileSystemProbe.EnumerateFiles(root, null, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SafetyGuard.IsSafeToDelete(file.FullName, allowedRoots, out var reason))
            {
                report.RecordSkipped(file.FullName, reason);
                continue;
            }

            long size;
            try
            {
                size = file.Length;
            }
            catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
            {
                size = 0;
            }

            if (TryDeleteFile(file.FullName, out var error))
            {
                freed += size;
            }
            else
            {
                report.RecordFailed(file.FullName, error);
            }
        }

        foreach (var directory in CollectDirectoriesDeepestFirst(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryRemoveEmptyDirectory(directory, allowedRoots, report);
        }

        TryRemoveEmptyDirectory(root, allowedRoots, report);
        return freed;
    }

    private static List<string> CollectDirectoriesDeepestFirst(string root, CancellationToken cancellationToken)
    {
        var all = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            foreach (var child in FileSystemProbe.SafeSubdirectories(current))
            {
                all.Add(child);
                pending.Push(child);
            }
        }

        // Deepest paths first so that parents are empty by the time we reach them.
        all.Sort((left, right) => right.Length.CompareTo(left.Length));
        return all;
    }

    private static void TryRemoveEmptyDirectory(string path, IReadOnlyList<string> allowedRoots, CleanReport report)
    {
        if (!SafetyGuard.IsSafeToDelete(path, allowedRoots, out var reason))
        {
            report.RecordSkipped(path, reason);
            return;
        }

        try
        {
            if (Directory.Exists(path) && !SafetyGuard.IsReparsePoint(path))
            {
                Directory.Delete(path, false);
            }
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            // A directory that refuses to go (still in use, or a file we could not delete)
            // is not an error worth surfacing on its own: the failed files already are.
        }
    }

    private static bool TryDeleteFile(string path, out string error)
    {
        try
        {
            if (!File.Exists(path))
            {
                error = string.Empty;
                return true;
            }

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }

            File.Delete(path);
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            error = FileSystemProbe.Describe(ex);
            return false;
        }
    }
}
