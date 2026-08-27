namespace CleaN.Core;

/// <summary>
/// A single file or directory that a module proposes to delete.
/// Targets are inert data: nothing is removed until <see cref="FileSweeper"/> runs.
/// </summary>
public sealed class CleanTarget
{
    public CleanTarget(string path, bool isDirectory, long sizeBytes, string? note = null)
    {
        Path = path;
        IsDirectory = isDirectory;
        SizeBytes = sizeBytes;
        Note = note;
    }

    /// <summary>Fully qualified path of the item.</summary>
    public string Path { get; }

    public bool IsDirectory { get; }

    /// <summary>Bytes recovered if the item is deleted (recursive for directories).</summary>
    public long SizeBytes { get; }

    /// <summary>Optional context shown in the preview list, e.g. the browser profile name.</summary>
    public string? Note { get; }

    public override string ToString() => Path;
}
