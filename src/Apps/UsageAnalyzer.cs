using CleaN.Core;

namespace CleaN.Apps;

/// <summary>
/// Joins the uninstall registry with Prefetch and UserAssist to answer one question per
/// application: when was this last actually run?
///
/// There is no list of known programs anywhere in here. Executables are discovered from
/// each application's own registry entry and install folder, which is what makes the result
/// work for anything the user has installed.
/// </summary>
public sealed class UsageAnalyzer
{
    /// <summary>Depth and count limits keep the scan quick on large install folders.</summary>
    private const int MaxExecutableSearchDepth = 2;
    private const int MaxExecutablesPerApp = 40;

    public sealed class AnalysisResult
    {
        public required IReadOnlyList<AppUsageInfo> Applications { get; init; }

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }

    public Task<AnalysisResult> AnalyzeAsync(CancellationToken cancellationToken) =>
        Task.Run(() => Analyze(cancellationToken), cancellationToken);

    public AnalysisResult Analyze(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var apps = InstalledAppsReader.Read();

        var prefetch = PrefetchReader.Read();
        if (prefetch.Problem is { } problem)
        {
            warnings.Add(problem);
        }

        if (!prefetch.Available)
        {
            warnings.Add("Without Prefetch, launch dates fall back to Explorer's own records and may be incomplete. " +
                         "Running cleaN as administrator gives the most accurate results.");
        }

        var userAssist = UserAssistReader.Read();
        var userAssistByExecutable = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in userAssist)
        {
            if (!userAssistByExecutable.TryGetValue(entry.ExecutableName, out var existing) || entry.LastRun > existing)
            {
                userAssistByExecutable[entry.ExecutableName] = entry.LastRun;
            }
        }

        var results = new List<AppUsageInfo>(apps.Count);

        foreach (var app in apps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var executables = CandidateExecutables(app, cancellationToken);
            DateTime? best = null;
            var evidence = UsageEvidence.Unknown;

            foreach (var executable in executables)
            {
                if (prefetch.LastRunByExecutable.TryGetValue(executable, out var prefetchRun) &&
                    (best is null || prefetchRun > best))
                {
                    best = prefetchRun;
                    evidence = UsageEvidence.Prefetch;
                }

                if (userAssistByExecutable.TryGetValue(executable, out var shellRun) && (best is null || shellRun > best))
                {
                    best = shellRun;
                    evidence = UsageEvidence.UserAssist;
                }
            }

            // Programs launched by full path from the shell, matched by install folder.
            if (app.InstallLocation.Length > 0)
            {
                foreach (var entry in userAssist)
                {
                    if (entry.Path.StartsWith(app.InstallLocation, StringComparison.OrdinalIgnoreCase) &&
                        (best is null || entry.LastRun > best))
                    {
                        best = entry.LastRun;
                        evidence = UsageEvidence.UserAssist;
                    }
                }
            }

            if (best is null && app.InstallDate is { } installed)
            {
                best = installed;
                evidence = UsageEvidence.InstallDate;
            }

            results.Add(new AppUsageInfo { App = app, LastUsed = best, Evidence = evidence });
        }

        // Longest unused first: that is the order the user actually wants to review.
        results.Sort((left, right) =>
        {
            var leftDate = left.LastUsed ?? DateTime.MinValue;
            var rightDate = right.LastUsed ?? DateTime.MinValue;
            return leftDate.CompareTo(rightDate);
        });

        return new AnalysisResult { Applications = results, Warnings = warnings };
    }

    /// <summary>Executable file names that plausibly belong to this application.</summary>
    private static IReadOnlyCollection<string> CandidateExecutables(InstalledApp app, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddExecutable(names, app.DisplayIcon);
        AddExecutable(names, app.UninstallString);
        AddExecutable(names, app.QuietUninstallString);

        if (app.InstallLocation.Length > 0 && Directory.Exists(app.InstallLocation))
        {
            CollectExecutables(app.InstallLocation, names, 0, cancellationToken);
        }

        return names;
    }

    private static void CollectExecutables(string directory, ISet<string> names, int depth,
        CancellationToken cancellationToken)
    {
        if (depth > MaxExecutableSearchDepth || names.Count >= MaxExecutablesPerApp)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.exe"))
            {
                names.Add(Path.GetFileName(file));
                if (names.Count >= MaxExecutablesPerApp)
                {
                    return;
                }
            }
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            return;
        }

        foreach (var child in FileSystemProbe.SafeSubdirectories(directory))
        {
            CollectExecutables(child, names, depth + 1, cancellationToken);
        }
    }

    /// <summary>Pulls "app.exe" out of a registry command line such as "C:\...\app.exe" /uninstall.</summary>
    private static void AddExecutable(ISet<string> names, string commandLine)
    {
        if (commandLine.Length == 0)
        {
            return;
        }

        var path = CommandLine.ExecutablePath(commandLine);
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(path);

            // The uninstaller itself is not evidence that the application was used.
            if (name.Length > 0 && !name.Contains("unins", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase))
            {
                names.Add(name);
            }
        }
    }
}
