using System.Diagnostics;

namespace CleaN.Apps;

/// <summary>
/// Hands an application's own uninstaller to Windows. cleaN never removes a program itself
/// and never starts an uninstall without the user asking for it explicitly.
/// </summary>
public static class AppUninstaller
{
    public static bool TryLaunch(InstalledApp app, out string error)
    {
        error = string.Empty;

        if (!app.CanUninstall)
        {
            error = "This application does not register an uninstall command.";
            return false;
        }

        CommandLine.Split(app.EffectiveUninstallCommand, out var executable, out var arguments);
        if (executable.Length == 0)
        {
            error = "The uninstall command stored in the registry could not be interpreted.";
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = true,
            };

            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException
                                       or System.IO.FileNotFoundException)
        {
            error = ex.Message;
            return false;
        }
    }
}
