using System.Diagnostics;
using CleaN.Core;

namespace CleaN.Modules;

public enum BrowserEngine
{
    /// <summary>Chrome, Edge, Brave, Vivaldi, Opera, Chromium...</summary>
    Chromium,

    /// <summary>Firefox and its forks.</summary>
    Gecko,
}

/// <summary>One profile of one installed browser.</summary>
public sealed class BrowserProfile
{
    public BrowserProfile(string browserName, string processName, string profileName, string dataDirectory,
        string cacheDirectory, BrowserEngine engine)
    {
        BrowserName = browserName;
        ProcessName = processName;
        ProfileName = profileName;
        DataDirectory = dataDirectory;
        CacheDirectory = cacheDirectory;
        Engine = engine;
    }

    public string BrowserName { get; }

    /// <summary>Executable name without extension, used to warn when the browser is running.</summary>
    public string ProcessName { get; }

    public string ProfileName { get; }

    /// <summary>Where cookies and history live (roaming profile for Firefox).</summary>
    public string DataDirectory { get; }

    /// <summary>Where the on-disk cache lives (local profile for Firefox).</summary>
    public string CacheDirectory { get; }

    public BrowserEngine Engine { get; }

    public string DisplayName => $"{BrowserName} - {ProfileName}";
}

/// <summary>
/// Discovers installed browsers by probing their well-known profile locations. Nothing is
/// hardcoded about *which* profiles exist: those are enumerated from disk, so extra
/// profiles ("Profile 1", "Profile 2"...) are picked up automatically.
/// </summary>
public static class BrowserCatalog
{
    private sealed record ChromiumBrowser(string Name, string ProcessName, string RelativeUserData, bool Roaming,
        bool SingleProfile = false);

    private sealed record GeckoBrowser(string Name, string ProcessName, string RelativeProfiles);

    private static readonly ChromiumBrowser[] ChromiumBrowsers =
    {
        new("Google Chrome", "chrome", @"Google\Chrome\User Data", false),
        new("Google Chrome Beta", "chrome", @"Google\Chrome Beta\User Data", false),
        new("Microsoft Edge", "msedge", @"Microsoft\Edge\User Data", false),
        new("Brave", "brave", @"BraveSoftware\Brave-Browser\User Data", false),
        new("Vivaldi", "vivaldi", @"Vivaldi\User Data", false),
        new("Chromium", "chrome", @"Chromium\User Data", false),
        new("Yandex Browser", "browser", @"Yandex\YandexBrowser\User Data", false),
        new("Opera", "opera", @"Opera Software\Opera Stable", true, SingleProfile: true),
        new("Opera GX", "opera", @"Opera Software\Opera GX Stable", true, SingleProfile: true),
    };

    private static readonly GeckoBrowser[] GeckoBrowsers =
    {
        new("Mozilla Firefox", "firefox", @"Mozilla\Firefox\Profiles"),
        new("Waterfox", "waterfox", @"Waterfox\Profiles"),
        new("LibreWolf", "librewolf", @"librewolf\Profiles"),
    };

    /// <summary>Every profile of every browser found on this machine.</summary>
    public static IReadOnlyList<BrowserProfile> Discover()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var profiles = new List<BrowserProfile>();

        foreach (var browser in ChromiumBrowsers)
        {
            var userData = Path.Combine(browser.Roaming ? roaming : local, browser.RelativeUserData);
            if (!Directory.Exists(userData))
            {
                continue;
            }

            if (browser.SingleProfile)
            {
                profiles.Add(new BrowserProfile(browser.Name, browser.ProcessName, "Default", userData, userData,
                    BrowserEngine.Chromium));
                continue;
            }

            foreach (var directory in FileSystemProbe.SafeSubdirectories(userData))
            {
                // A Chromium profile is any folder holding a "Preferences" file.
                if (File.Exists(Path.Combine(directory, "Preferences")))
                {
                    profiles.Add(new BrowserProfile(browser.Name, browser.ProcessName,
                        Path.GetFileName(directory), directory, directory, BrowserEngine.Chromium));
                }
            }
        }

        foreach (var browser in GeckoBrowsers)
        {
            var roamingProfiles = Path.Combine(roaming, browser.RelativeProfiles);
            var localProfiles = Path.Combine(local, browser.RelativeProfiles);
            if (!Directory.Exists(roamingProfiles))
            {
                continue;
            }

            foreach (var directory in FileSystemProbe.SafeSubdirectories(roamingProfiles))
            {
                var name = Path.GetFileName(directory);
                var cache = Path.Combine(localProfiles, name);
                profiles.Add(new BrowserProfile(browser.Name, browser.ProcessName, name, directory,
                    Directory.Exists(cache) ? cache : directory, BrowserEngine.Gecko));
            }
        }

        return profiles;
    }

    /// <summary>Roots the browser modules are allowed to delete inside.</summary>
    public static IReadOnlyList<string> DiscoverRoots()
    {
        var roots = new List<string>();
        foreach (var profile in Discover())
        {
            AddRoot(roots, profile.DataDirectory);
            AddRoot(roots, profile.CacheDirectory);
        }

        return roots;
    }

    /// <summary>Names of the browsers that are currently running, so the UI can warn about locked files.</summary>
    public static IReadOnlyList<string> RunningBrowsers(IEnumerable<BrowserProfile> profiles)
    {
        var running = new List<string>();
        foreach (var group in profiles.GroupBy(profile => profile.BrowserName))
        {
            var processName = group.First().ProcessName;
            try
            {
                if (Process.GetProcessesByName(processName).Length > 0)
                {
                    running.Add(group.Key);
                }
            }
            catch (InvalidOperationException)
            {
                // Process enumeration can race with a process exiting; not worth reporting.
            }
        }

        return running;
    }

    private static void AddRoot(ICollection<string> roots, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            var normalized = SafetyGuard.Normalize(path);
            if (!roots.Contains(normalized))
            {
                roots.Add(normalized);
            }
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            // Ignore unusable paths.
        }
    }
}
