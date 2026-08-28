using System.Text.Json;
using System.Text.Json.Serialization;

namespace CleaN.Core;

public enum AppTheme
{
    Light,
    Dark,
}

/// <summary>User preferences, persisted to %APPDATA%\cleaN\settings.json.</summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string SettingsFile { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cleaN", "settings.json");

    /// <summary>White is the default theme, per the design brief.</summary>
    public AppTheme Theme { get; set; } = AppTheme.Light;

    /// <summary>Preview mode is on by default and has to be turned off deliberately.</summary>
    public bool PreviewOnly { get; set; } = true;

    /// <summary>Save a plain-text log file for every run. Off by default; turn it on in the header or the Report tab.</summary>
    public bool WriteLog { get; set; } = false;

    /// <summary>Module ids the user ticked. Null means "use each module's own default".</summary>
    public List<string>? SelectedModules { get; set; }

    /// <summary>Roots used by the empty-folder scanner; empty means "user profile only".</summary>
    public List<string> EmptyFolderRoots { get; set; } = new();

    /// <summary>Days without use before an application is flagged as unused.</summary>
    public int UnusedAppThresholdDays { get; set; } = 180;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex) || ex is JsonException)
        {
            // A corrupt settings file must never stop the app from starting.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(this, SerializerOptions));
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            // Losing preferences is not worth an error dialog.
        }
    }
}
