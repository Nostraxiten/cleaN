using System.Globalization;
using System.Text;

namespace CleaN.Core;

/// <summary>
/// Writes one plain-text log per clean run under %LOCALAPPDATA%\cleaN\logs, listing every
/// file removed and the total space recovered, so a run can always be audited afterwards.
/// </summary>
public static class CleanLogger
{
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cleaN", "logs");

    public static string? Write(IReadOnlyList<CleanReport> reports, bool previewOnly)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var file = Path.Combine(LogDirectory, $"clean-{stamp}.log");
            File.WriteAllText(file, Render(reports, previewOnly), Encoding.UTF8);
            return file;
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            return null;
        }
    }

    public static string Render(IReadOnlyList<CleanReport> reports, bool previewOnly)
    {
        var builder = new StringBuilder();
        builder.AppendLine("cleaN - clean report");
        builder.AppendLine("Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
        builder.AppendLine("Mode: " + (previewOnly ? "PREVIEW (nothing was deleted)" : "CLEAN (files were deleted)"));
        builder.AppendLine(new string('-', 72));

        long grandTotal = 0;
        var totalItems = 0;

        foreach (var report in reports)
        {
            grandTotal += report.BytesFreed;
            totalItems += report.Deleted.Count;

            builder.AppendLine();
            builder.AppendLine($"[{report.ModuleId}] {report.ToSummary()}");

            foreach (var path in report.Deleted)
            {
                builder.AppendLine("  - " + path);
            }

            foreach (var failure in report.Failed)
            {
                builder.AppendLine("  ! could not delete: " + failure);
            }

            foreach (var skipped in report.Skipped)
            {
                builder.AppendLine("  # protected, left untouched: " + skipped);
            }
        }

        builder.AppendLine();
        builder.AppendLine(new string('-', 72));
        builder.AppendLine($"TOTAL: {totalItems} item(s), {SizeFormatter.Format(grandTotal)} " +
                           (previewOnly ? "would be freed." : "freed."));
        return builder.ToString();
    }

    /// <summary>Existing log files, newest first.</summary>
    public static IReadOnlyList<FileInfo> RecentLogs(int max = 50)
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                return Array.Empty<FileInfo>();
            }

            var files = new DirectoryInfo(LogDirectory).GetFiles("clean-*.log");
            Array.Sort(files, (left, right) => right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc));
            return files.Length <= max ? files : files[..max];
        }
        catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
        {
            return Array.Empty<FileInfo>();
        }
    }
}
