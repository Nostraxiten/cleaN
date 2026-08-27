using System.Text;
using Microsoft.Win32;

namespace CleaN.Apps;

/// <summary>
/// Reads HKCU UserAssist, the per-user record Explorer keeps of programs launched from the
/// shell. Value names are ROT13 encoded paths; the binary payload holds a run count and the
/// last execution time as a FILETIME. This is the fallback (and cross-check) for Prefetch,
/// and unlike Prefetch it needs no elevation.
/// </summary>
public static class UserAssistReader
{
    private const string UserAssistPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist";

    /// <summary>Layout of the Windows 7 and later UserAssist payload.</summary>
    private const int ModernEntryLength = 68;
    private const int ModernRunCountOffset = 4;
    private const int ModernLastRunOffset = 60;

    public sealed class Entry
    {
        public required string Path { get; init; }

        public required string ExecutableName { get; init; }

        public DateTime LastRun { get; init; }

        public int RunCount { get; init; }
    }

    public static IReadOnlyList<Entry> Read()
    {
        var entries = new List<Entry>();

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(UserAssistPath);
            if (root is null)
            {
                return entries;
            }

            foreach (var guid in root.GetSubKeyNames())
            {
                using var counts = root.OpenSubKey(guid + @"\Count");
                if (counts is null)
                {
                    continue;
                }

                foreach (var valueName in counts.GetValueNames())
                {
                    var decoded = Rot13(valueName);
                    if (decoded.Length == 0 || counts.GetValue(valueName) is not byte[] data)
                    {
                        continue;
                    }

                    if (!TryParse(data, out var lastRun, out var runCount))
                    {
                        continue;
                    }

                    entries.Add(new Entry
                    {
                        Path = decoded,
                        ExecutableName = LeafName(decoded),
                        LastRun = lastRun,
                        RunCount = runCount,
                    });
                }
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // UserAssist is an optimisation, not a requirement.
        }

        return entries;
    }

    private static bool TryParse(byte[] data, out DateTime lastRun, out int runCount)
    {
        lastRun = default;
        runCount = 0;

        try
        {
            if (data.Length >= ModernEntryLength)
            {
                runCount = BitConverter.ToInt32(data, ModernRunCountOffset);
                var fileTime = BitConverter.ToInt64(data, ModernLastRunOffset);
                if (fileTime <= 0)
                {
                    return false;
                }

                lastRun = DateTime.FromFileTime(fileTime);
                return lastRun.Year is > 1990 and < 2200;
            }

            // Windows XP layout: session id, run count, then the FILETIME.
            if (data.Length >= 16)
            {
                runCount = BitConverter.ToInt32(data, 4);
                var fileTime = BitConverter.ToInt64(data, 8);
                if (fileTime <= 0)
                {
                    return false;
                }

                lastRun = DateTime.FromFileTime(fileTime);
                return lastRun.Year is > 1990 and < 2200;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        return false;
    }

    private static string LeafName(string path)
    {
        var separator = path.LastIndexOf('\\');
        return separator >= 0 && separator < path.Length - 1 ? path[(separator + 1)..] : path;
    }

    /// <summary>UserAssist value names are stored ROT13 encoded.</summary>
    private static string Rot13(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                >= 'a' and <= 'z' => (char)('a' + (character - 'a' + 13) % 26),
                >= 'A' and <= 'Z' => (char)('A' + (character - 'A' + 13) % 26),
                _ => character,
            });
        }

        return builder.ToString();
    }
}
