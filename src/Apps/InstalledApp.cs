namespace CleaN.Apps;

/// <summary>Where an application's uninstall entry was found.</summary>
public enum RegistryScope
{
    Machine64,
    Machine32,
    CurrentUser,
}

/// <summary>One entry of the Windows uninstall registry, normalised.</summary>
public sealed class InstalledApp
{
    public required string DisplayName { get; init; }

    public string Publisher { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string InstallLocation { get; init; } = string.Empty;

    public string UninstallString { get; init; } = string.Empty;

    public string QuietUninstallString { get; init; } = string.Empty;

    public string DisplayIcon { get; init; } = string.Empty;

    public DateTime? InstallDate { get; init; }

    /// <summary>Size reported by the installer. Often absent, occasionally wrong.</summary>
    public long EstimatedSizeBytes { get; init; }

    public RegistryScope Scope { get; init; }

    public string RegistryKeyName { get; init; } = string.Empty;

    /// <summary>The command cleaN hands to Windows when the user asks to uninstall.</summary>
    public string EffectiveUninstallCommand =>
        QuietUninstallString.Length > 0 ? QuietUninstallString : UninstallString;

    public bool CanUninstall => EffectiveUninstallCommand.Length > 0;
}
