using CleaN.Core;

namespace CleaN.Modules;

/// <summary>
/// Deletes browsing history, including the download list and the address bar suggestions
/// built from it. Off by default, for the same reason as cookies.
/// </summary>
public sealed class BrowserHistoryModule : BrowserDataModuleBase
{
    private static readonly string[] ChromiumHistoryFiles =
    {
        "History",
        "History-journal",
        "History Provider Cache",
        "Top Sites",
        "Top Sites-journal",
        "Visited Links",
        "Network Action Predictor",
        "Network Action Predictor-journal",
    };

    private static readonly string[] GeckoHistoryFiles =
    {
        "places.sqlite",
        "places.sqlite-wal",
        "places.sqlite-shm",
        "formhistory.sqlite",
    };

    public override string Id => "browser-history";

    public override string Name => "Browsing history";

    public override string Description =>
        "Visited pages, download list and address bar suggestions. WARNING: this cannot be undone.";

    public override bool EnabledByDefault => false;

    protected override void CollectTargets(BrowserProfile profile, List<CleanTarget> targets, List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (profile.Engine == BrowserEngine.Chromium)
        {
            AddFiles(profile, profile.DataDirectory, ChromiumHistoryFiles, targets);
            return;
        }

        AddFiles(profile, profile.DataDirectory, GeckoHistoryFiles, targets);
        AddFolderContents(profile, profile.DataDirectory, "sessionstore-backups", targets, warnings, cancellationToken);
    }
}
