namespace CleaN.Apps;

/// <summary>Splits the command lines stored in the uninstall registry into program and arguments.</summary>
public static class CommandLine
{
    /// <summary>The executable part of a command line, with quotes removed.</summary>
    public static string ExecutablePath(string commandLine)
    {
        Split(commandLine, out var executable, out _);
        return executable;
    }

    public static void Split(string commandLine, out string executable, out string arguments)
    {
        executable = string.Empty;
        arguments = string.Empty;

        var trimmed = commandLine.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        if (trimmed[0] == '"')
        {
            var closing = trimmed.IndexOf('"', 1);
            if (closing > 0)
            {
                executable = trimmed[1..closing];
                arguments = trimmed[(closing + 1)..].Trim();
                return;
            }

            executable = trimmed.Trim('"');
            return;
        }

        // Unquoted: the executable ends at ".exe" when present, otherwise at the first space.
        var extension = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (extension > 0)
        {
            executable = trimmed[..(extension + 4)];
            arguments = trimmed[(extension + 4)..].Trim();
            return;
        }

        var space = trimmed.IndexOf(' ');
        if (space > 0)
        {
            executable = trimmed[..space];
            arguments = trimmed[(space + 1)..].Trim();
            return;
        }

        executable = trimmed;
    }
}
