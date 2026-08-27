using System.Text;

namespace CleaN.Core;

/// <summary>Outcome of an actual (or simulated) clean run.</summary>
public sealed class CleanReport
{
    private readonly List<string> _deleted = new();
    private readonly List<string> _failed = new();
    private readonly List<string> _skipped = new();

    public CleanReport(string moduleId, bool previewOnly)
    {
        ModuleId = moduleId;
        PreviewOnly = previewOnly;
    }

    public string ModuleId { get; }

    public bool PreviewOnly { get; }

    public long BytesFreed { get; private set; }

    public IReadOnlyList<string> Deleted => _deleted;

    public IReadOnlyList<string> Failed => _failed;

    /// <summary>Items the safety guard refused to touch, with the reason.</summary>
    public IReadOnlyList<string> Skipped => _skipped;

    public void RecordDeleted(string path, long bytes)
    {
        _deleted.Add(path);
        BytesFreed += bytes;
    }

    public void RecordFailed(string path, string reason) => _failed.Add($"{path} -- {reason}");

    public void RecordSkipped(string path, string reason) => _skipped.Add($"{path} -- {reason}");

    public string ToSummary()
    {
        var verb = PreviewOnly ? "would free" : "freed";
        var builder = new StringBuilder();
        builder.Append(_deleted.Count).Append(" item(s), ").Append(verb).Append(' ')
               .Append(SizeFormatter.Format(BytesFreed));

        if (_failed.Count > 0)
        {
            builder.Append(" - ").Append(_failed.Count).Append(" failed");
        }

        if (_skipped.Count > 0)
        {
            builder.Append(" - ").Append(_skipped.Count).Append(" protected");
        }

        return builder.ToString();
    }
}
