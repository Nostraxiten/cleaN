namespace CleaN.Core;

/// <summary>
/// The last line of defence before anything is deleted.
///
/// The rule is deliberately restrictive and works as a whitelist, not a blacklist:
/// a path may only be removed when it lives *strictly below* a root that the module
/// explicitly declared, is not inside a protected subtree, is not a protected folder
/// itself, and sits at least <see cref="MinimumSegmentDepth"/> levels below the drive root.
/// Anything that fails a single check is skipped and reported, never deleted.
/// </summary>
public static class SafetyGuard
{
    /// <summary>C:\a\b is the shallowest path cleaN will ever delete.</summary>
    public const int MinimumSegmentDepth = 2;

    private static readonly HashSet<string> NeverDelete = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> ForbiddenSubtrees = new();
    private static readonly List<string> WindowsAllowedSubtrees = new();
    private static readonly string WindowsDirectory;

    static SafetyGuard()
    {
        WindowsDirectory = SafeFolder(Environment.SpecialFolder.Windows);

        // Locations inside %WINDIR% that are safe to clean. Everything else under
        // %WINDIR% is off limits, which is far safer than enumerating what to avoid.
        foreach (var relative in new[] { "Temp", "Logs", @"SoftwareDistribution\Download", "Downloaded Program Files" })
        {
            if (WindowsDirectory.Length > 0)
            {
                WindowsAllowedSubtrees.Add(Normalize(Path.Combine(WindowsDirectory, relative)));
            }
        }

        // Folders that must survive no matter what, even when they are empty.
        foreach (var folder in new[]
                 {
                     Environment.SpecialFolder.Windows, Environment.SpecialFolder.System,
                     Environment.SpecialFolder.SystemX86, Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.CommonProgramFiles,
                     Environment.SpecialFolder.CommonProgramFilesX86, Environment.SpecialFolder.CommonApplicationData,
                     Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.ApplicationData,
                     Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolder.Desktop,
                     Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyDocuments,
                     Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyPictures,
                     Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.Favorites,
                     Environment.SpecialFolder.Fonts, Environment.SpecialFolder.StartMenu,
                     Environment.SpecialFolder.Programs, Environment.SpecialFolder.Startup,
                     Environment.SpecialFolder.Templates, Environment.SpecialFolder.SendTo,
                     Environment.SpecialFolder.Recent, Environment.SpecialFolder.History,
                     Environment.SpecialFolder.InternetCache, Environment.SpecialFolder.Cookies,
                 })
        {
            Add(NeverDelete, SafeFolder(folder));
        }

        // The temp folders themselves stay; only their contents are fair game.
        Add(NeverDelete, SafeNormalize(Path.GetTempPath()));
        foreach (var allowed in WindowsAllowedSubtrees)
        {
            NeverDelete.Add(allowed);
        }

        // Drive roots.
        foreach (var root in EnumerateDriveRoots())
        {
            NeverDelete.Add(root);
        }

        // Subtrees nothing may ever touch.
        var systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        foreach (var candidate in new[]
                 {
                     SafeFolder(Environment.SpecialFolder.ProgramFiles),
                     SafeFolder(Environment.SpecialFolder.ProgramFilesX86),
                     Combine(systemDrive + Path.DirectorySeparatorChar, "$Recycle.Bin"),
                     Combine(systemDrive + Path.DirectorySeparatorChar, "System Volume Information"),
                     Combine(systemDrive + Path.DirectorySeparatorChar, "Recovery"),
                     Combine(systemDrive + Path.DirectorySeparatorChar, "PerfLogs"),
                     Combine(systemDrive + Path.DirectorySeparatorChar, "Boot"),
                     Combine(systemDrive + Path.DirectorySeparatorChar, "EFI"),
                     Combine(SafeFolder(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu"),
                     Combine(SafeFolder(Environment.SpecialFolder.CommonApplicationData), "Package Cache"),
                     SafeNormalize(AppContext.BaseDirectory),
                 })
        {
            Add(ForbiddenSubtrees, candidate);
        }

        // Cloud sync roots: removing an "empty" folder there propagates the delete online.
        foreach (var variable in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
        {
            Add(ForbiddenSubtrees, SafeNormalize(Environment.GetEnvironmentVariable(variable)));
        }
    }

    /// <summary>Protected locations, for display in the UI.</summary>
    public static IReadOnlyList<string> ProtectedLocations
    {
        get
        {
            var all = new List<string>(ForbiddenSubtrees);
            all.AddRange(NeverDelete);
            all.Sort(StringComparer.OrdinalIgnoreCase);
            return all;
        }
    }

    /// <summary>
    /// Decides whether <paramref name="path"/> may be deleted given the roots the calling
    /// module declared. <paramref name="reason"/> explains every refusal.
    /// </summary>
    public static bool IsSafeToDelete(string path, IReadOnlyList<string> allowedRoots, out string reason)
    {
        string normalized;
        try
        {
            normalized = Normalize(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            reason = "the path could not be resolved";
            return false;
        }

        if (!Path.IsPathRooted(normalized))
        {
            reason = "the path is not absolute";
            return false;
        }

        if (SegmentDepth(normalized) < MinimumSegmentDepth)
        {
            reason = $"the path is too close to the drive root (fewer than {MinimumSegmentDepth} levels deep)";
            return false;
        }

        if (NeverDelete.Contains(normalized))
        {
            reason = "it is a protected system or user folder";
            return false;
        }

        foreach (var forbidden in ForbiddenSubtrees)
        {
            if (IsSameOrUnder(normalized, forbidden))
            {
                reason = $"it lives inside the protected location {forbidden}";
                return false;
            }
        }

        if (WindowsDirectory.Length > 0 && IsSameOrUnder(normalized, WindowsDirectory) && !IsInsideAllowedWindowsArea(normalized))
        {
            reason = "it is inside the Windows directory but outside the areas cleaN is allowed to clean";
            return false;
        }

        var insideDeclaredRoot = false;
        foreach (var root in allowedRoots)
        {
            if (IsUnder(normalized, root))
            {
                insideDeclaredRoot = true;
                break;
            }
        }

        if (!insideDeclaredRoot)
        {
            reason = "it is outside every location this module is allowed to work in";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>True when the path is a junction or symbolic link, which cleaN never follows.</summary>
    public static bool IsReparsePoint(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // If we cannot even read the attributes, treat it as something to leave alone.
            return true;
        }
    }

    public static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.Length > 3 && (full.EndsWith(Path.DirectorySeparatorChar) || full.EndsWith(Path.AltDirectorySeparatorChar)))
        {
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return full;
    }

    /// <summary>True when <paramref name="candidate"/> sits strictly below <paramref name="root"/>.</summary>
    public static bool IsUnder(string candidate, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        string normalizedCandidate, normalizedRoot;
        try
        {
            normalizedCandidate = Normalize(candidate);
            normalizedRoot = Normalize(root);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (normalizedCandidate.Length <= normalizedRoot.Length)
        {
            return false;
        }

        if (!normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // "C:\" already ends with a separator; "C:\Windows" needs one to avoid matching "C:\WindowsOld".
        return normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
               || normalizedCandidate[normalizedRoot.Length] == Path.DirectorySeparatorChar;
    }

    public static bool IsSameOrUnder(string candidate, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        try
        {
            if (string.Equals(Normalize(candidate), Normalize(root), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return IsUnder(candidate, root);
    }

    private static bool IsInsideAllowedWindowsArea(string normalized)
    {
        foreach (var allowed in WindowsAllowedSubtrees)
        {
            if (IsSameOrUnder(normalized, allowed))
            {
                return true;
            }
        }

        return false;
    }

    private static int SegmentDepth(string normalized)
    {
        var root = Path.GetPathRoot(normalized) ?? string.Empty;
        if (normalized.Length <= root.Length)
        {
            return 0;
        }

        var rest = normalized[root.Length..];
        return rest.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static IEnumerable<string> EnumerateDriveRoots()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (var drive in drives)
        {
            var name = drive.Name;
            if (!string.IsNullOrEmpty(name))
            {
                yield return name;
            }
        }
    }

    private static string SafeFolder(Environment.SpecialFolder folder)
    {
        try
        {
            return SafeNormalize(Environment.GetFolderPath(folder));
        }
        catch (Exception ex) when (ex is ArgumentException or PlatformNotSupportedException)
        {
            return string.Empty;
        }
    }

    private static string SafeNormalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Normalize(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Empty;
        }
    }

    private static string Combine(string root, string relative) =>
        string.IsNullOrEmpty(root) ? string.Empty : SafeNormalize(Path.Combine(root, relative));

    private static void Add(ICollection<string> target, string value)
    {
        if (!string.IsNullOrEmpty(value) && !target.Contains(value))
        {
            target.Add(value);
        }
    }
}
