using CleaN.Core;

namespace CleaN.Apps;

/// <summary>
/// Reads the last execution time of every program Windows has prefetched.
///
/// A .pf file is named EXECUTABLE.EXE-HASH.pf and Windows rewrites it every time the
/// program runs, so the file's last write time is a reliable "last launched" stamp without
/// having to decode the compressed prefetch format. Reading the folder requires elevation.
/// </summary>
public static class PrefetchReader
{
    public static string PrefetchDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");

    public sealed class Result
    {
        /// <summary>Executable file name (with extension) to the most recent launch seen.</summary>
        public Dictionary<string, DateTime> LastRunByExecutable { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Available { get; set; }

        public string? Problem { get; set; }
    }

    public static Result Read()
    {
        var result = new Result();

        if (!Directory.Exists(PrefetchDirectory))
        {
            result.Problem = "The Prefetch folder does not exist. Prefetching may be disabled on this system.";
            return result;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(PrefetchDirectory, "*.pf");
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            result.Problem = $"Prefetch could not be read: {FileSystemProbe.Describe(ex)}";
            return result;
        }

        result.Available = true;

        foreach (var file in files)
        {
            var executable = ExecutableNameFrom(Path.GetFileNameWithoutExtension(file));
            if (executable.Length == 0)
            {
                continue;
            }

            DateTime lastRun;
            try
            {
                lastRun = File.GetLastWriteTime(file);
            }
            catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
            {
                continue;
            }

            if (!result.LastRunByExecutable.TryGetValue(executable, out var existing) || lastRun > existing)
            {
                result.LastRunByExecutable[executable] = lastRun;
            }
        }

        if (files.Length == 0)
        {
            result.Problem = "The Prefetch folder is empty, so launch dates come from other sources.";
        }

        return result;
    }

    /// <summary>"CHROME.EXE-A1B2C3D4" becomes "CHROME.EXE".</summary>
    private static string ExecutableNameFrom(string prefetchName)
    {
        var separator = prefetchName.LastIndexOf('-');
        return separator <= 0 ? prefetchName : prefetchName[..separator];
    }
}
