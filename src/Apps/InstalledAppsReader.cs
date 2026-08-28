using Microsoft.Win32;

namespace CleaN.Apps;

/// <summary>
/// Reads every installed application from the uninstall registry keys, for both bitnesses
/// and for the current user. Nothing is hardcoded: whatever is installed shows up here,
/// which is what makes the unused-application detection universal.
/// </summary>
public static class InstalledAppsReader
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public static IReadOnlyList<InstalledApp> Read()
    {
        var apps = new List<InstalledApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Collect(RegistryHive.LocalMachine, RegistryView.Registry64, RegistryScope.Machine64, apps, seen);
        Collect(RegistryHive.LocalMachine, RegistryView.Registry32, RegistryScope.Machine32, apps, seen);
        Collect(RegistryHive.CurrentUser, RegistryView.Default, RegistryScope.CurrentUser, apps, seen);

        apps.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.CurrentCultureIgnoreCase));
        return apps;
    }

    private static void Collect(RegistryHive hive, RegistryView view, RegistryScope scope,
        ICollection<InstalledApp> apps, ISet<string> seen)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstallKey = baseKey.OpenSubKey(UninstallPath);
            if (uninstallKey is null)
            {
                return;
            }

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                using var entry = uninstallKey.OpenSubKey(subKeyName);
                if (entry is null)
                {
                    continue;
                }

                var app = Parse(entry, subKeyName, scope);
                if (app is null)
                {
                    continue;
                }

                // The same product can appear in both the 32 and 64 bit views.
                var key = app.DisplayName + "|" + app.Version;
                if (seen.Add(key))
                {
                    apps.Add(app);
                }
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // A hive we cannot read simply contributes nothing.
        }
    }

    private static InstalledApp? Parse(RegistryKey entry, string subKeyName, RegistryScope scope)
    {
        var displayName = ReadString(entry, "DisplayName");
        if (displayName.Length == 0)
        {
            return null;
        }

        // Hidden components, Windows updates and installer sub-entries are not applications.
        if (ReadInt(entry, "SystemComponent") == 1)
        {
            return null;
        }

        if (ReadString(entry, "ParentKeyName").Length > 0)
        {
            return null;
        }

        var releaseType = ReadString(entry, "ReleaseType");
        if (releaseType is "Security Update" or "Update" or "Hotfix" or "ServicePack")
        {
            return null;
        }

        return new InstalledApp
        {
            DisplayName = displayName,
            Publisher = ReadString(entry, "Publisher"),
            Version = ReadString(entry, "DisplayVersion"),
            InstallLocation = ReadString(entry, "InstallLocation").Trim('"'),
            UninstallString = ReadString(entry, "UninstallString"),
            QuietUninstallString = ReadString(entry, "QuietUninstallString"),
            DisplayIcon = ReadString(entry, "DisplayIcon"),
            InstallDate = ParseInstallDate(ReadString(entry, "InstallDate")),
            EstimatedSizeBytes = ReadInt(entry, "EstimatedSize") * 1024L,
            Scope = scope,
            RegistryKeyName = subKeyName,
        };
    }

    private static DateTime? ParseInstallDate(string raw)
    {
        if (raw.Length == 8 && DateTime.TryParseExact(raw, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string ReadString(RegistryKey key, string name)
    {
        try
        {
            return key.GetValue(name) as string ?? string.Empty;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static int ReadInt(RegistryKey key, string name)
    {
        try
        {
            return key.GetValue(name) is int value ? value : 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
