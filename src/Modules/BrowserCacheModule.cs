using CleaN.Core;

namespace CleaN.Modules;

/// <summary>
/// Clears the on-disk cache of every installed browser profile. This only costs the user a
/// slower first page load, so it is enabled by default.
/// </summary>
public sealed class BrowserCacheModule : BrowserDataModuleBase
{
    private static readonly string[] ChromiumCacheFolders =
    {
        "Cache",
        "Code Cache",
        "GPUCache",
        "GrShaderCache",
        "ShaderCache",
        "DawnCache",
        "DawnGraphiteCache",
        "DawnWebGPUCache",
        @"Service Worker\CacheStorage",
        @"Service Worker\ScriptCache",
        "Media Cache",
        "Application Cache",
    };

    private static readonly string[] GeckoCacheFolders =
    {
        "cache2",
        "startupCache",
        "thumbnails",
        "jumpListCache",
        "shader-cache",
        "OfflineCache",
    };

    public override string Id => "browser-cache";

    public override string Name => "Browser cache";

    public override string Description =>
        "Cached pages, images and scripts. Safe to remove: you stay logged in and keep your history.";

    protected override void CollectTargets(BrowserProfile profile, List<CleanTarget> targets, List<string> warnings,
        CancellationToken cancellationToken)
    {
        var folders = profile.Engine == BrowserEngine.Chromium ? ChromiumCacheFolders : GeckoCacheFolders;
        foreach (var folder in folders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddFolderContents(profile, profile.CacheDirectory, folder, targets, warnings, cancellationToken);
        }
    }
}
