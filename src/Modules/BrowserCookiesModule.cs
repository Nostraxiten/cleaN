using CleaN.Core;

namespace CleaN.Modules;

/// <summary>
/// Deletes browser cookies. Off by default: clearing cookies signs the user out of every
/// site, which is never something a cleaner should decide on its own.
/// </summary>
public sealed class BrowserCookiesModule : BrowserDataModuleBase
{
    private static readonly string[] ChromiumCookieFiles =
    {
        @"Network\Cookies",
        @"Network\Cookies-journal",
        "Cookies",
        "Cookies-journal",
    };

    private static readonly string[] GeckoCookieFiles =
    {
        "cookies.sqlite",
        "cookies.sqlite-wal",
        "cookies.sqlite-shm",
    };

    public override string Id => "browser-cookies";

    public override string Name => "Browser cookies";

    public override string Description =>
        "Cookies for every profile. WARNING: this signs you out of the sites you are logged into.";

    public override bool EnabledByDefault => false;

    protected override void CollectTargets(BrowserProfile profile, List<CleanTarget> targets, List<string> warnings,
        CancellationToken cancellationToken)
    {
        var files = profile.Engine == BrowserEngine.Chromium ? ChromiumCookieFiles : GeckoCookieFiles;
        AddFiles(profile, profile.DataDirectory, files, targets);
    }
}
