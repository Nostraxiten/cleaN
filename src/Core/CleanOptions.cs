namespace CleaN.Core;

/// <summary>Runtime switches that apply to every delete operation.</summary>
public sealed class CleanOptions
{
    /// <summary>
    /// When true (the default) nothing is deleted: cleaN only reports what it would remove.
    /// The UI has to opt out of this explicitly, per the "preview first" rule.
    /// </summary>
    public bool PreviewOnly { get; init; } = true;

    /// <summary>Write a log file for the operation under %LOCALAPPDATA%\cleaN\logs.</summary>
    public bool WriteLog { get; init; } = true;
}
