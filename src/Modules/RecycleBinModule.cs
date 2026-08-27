using System.Runtime.InteropServices;
using CleaN.Core;
using CleaN.Interop;

namespace CleaN.Modules;

/// <summary>
/// Empties the recycle bin through the shell API rather than by deleting $Recycle.Bin by
/// hand, which is what keeps the operation correct across drives and user accounts.
/// </summary>
public sealed class RecycleBinModule : CleanerModuleBase
{
    public override string Id => "recycle-bin";

    public override string Name => "Recycle Bin";

    public override string Description =>
        "Permanently removes everything currently in the Recycle Bin, on every drive.";

    public override CleanCategory Category => CleanCategory.RecycleBin;

    /// <summary>
    /// Off by default: the recycle bin is the user's own safety net, and emptying it is
    /// exactly the kind of thing that should be a conscious click.
    /// </summary>
    public override bool EnabledByDefault => false;

    /// <summary>The shell API does the deleting, so no file system roots are needed.</summary>
    protected override IReadOnlyList<string> BuildAllowedRoots() => Array.Empty<string>();

    protected override ScanResult Scan(CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var targets = new List<CleanTarget>();

        foreach (var drive in ReadyDrives())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryQuery(drive.Name, out var size, out var items) || items <= 0)
            {
                continue;
            }

            targets.Add(new CleanTarget($"Recycle Bin on {drive.Name}", true, size,
                items == 1 ? "1 item" : $"{items} items"));
        }

        if (targets.Count == 0)
        {
            warnings.Add("The Recycle Bin is already empty.");
        }

        return new ScanResult(Id, targets, warnings);
    }

    public override Task<CleanReport> CleanAsync(ScanResult scan, CleanOptions options, IProgress<string>? progress,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        var report = new CleanReport(Id, options.PreviewOnly);

        foreach (var target in scan.Targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (options.PreviewOnly)
            {
                report.RecordDeleted(target.Path, target.SizeBytes);
                continue;
            }

            progress?.Report(target.Path);
            var drive = target.Path[(target.Path.LastIndexOf(" on ", StringComparison.Ordinal) + 4)..];
            var result = NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, drive,
                NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND);

            if (result == NativeMethods.S_OK)
            {
                report.RecordDeleted(target.Path, target.SizeBytes);
            }
            else
            {
                report.RecordFailed(target.Path, $"the shell returned error 0x{result:X8}");
            }
        }

        return report;
    }, cancellationToken);

    private static IEnumerable<DriveInfo> ReadyDrives()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (var drive in drives)
        {
            var usable = false;
            try
            {
                usable = drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable;
            }
            catch (Exception ex) when (FileSystemProbe.IsExpected(ex))
            {
                usable = false;
            }

            if (usable)
            {
                yield return drive;
            }
        }
    }

    private static bool TryQuery(string root, out long size, out long items)
    {
        size = 0;
        items = 0;

        var info = new NativeMethods.SHQUERYRBINFO { cbSize = Marshal.SizeOf<NativeMethods.SHQUERYRBINFO>() };
        try
        {
            if (NativeMethods.SHQueryRecycleBin(root, ref info) != NativeMethods.S_OK)
            {
                return false;
            }
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }

        size = info.i64Size;
        items = info.i64NumItems;
        return true;
    }
}
