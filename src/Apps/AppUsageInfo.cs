namespace CleaN.Apps;

/// <summary>How cleaN worked out when an application last ran.</summary>
public enum UsageEvidence
{
    /// <summary>No launch record at all.</summary>
    Unknown,

    /// <summary>C:\Windows\Prefetch, the most reliable source.</summary>
    Prefetch,

    /// <summary>HKCU UserAssist, for programs started from the shell.</summary>
    UserAssist,

    /// <summary>Nothing but the installation date, i.e. no launch was ever recorded.</summary>
    InstallDate,
}

/// <summary>An installed application joined with its usage history.</summary>
public sealed class AppUsageInfo
{
    public required InstalledApp App { get; init; }

    public DateTime? LastUsed { get; init; }

    public UsageEvidence Evidence { get; init; }

    /// <summary>Null when nothing at all is known about this application's usage.</summary>
    public int? DaysSinceLastUse => LastUsed is null ? null : Math.Max(0, (int)(DateTime.Now - LastUsed.Value).TotalDays);

    public bool IsUnused(int thresholdDays) => DaysSinceLastUse is { } days && days >= thresholdDays;

    public string EvidenceDescription => Evidence switch
    {
        UsageEvidence.Prefetch => "last launch recorded by Windows Prefetch",
        UsageEvidence.UserAssist => "last launch recorded by Explorer (UserAssist)",
        UsageEvidence.InstallDate => "never seen running; date is the installation date",
        _ => "no usage information available",
    };
}
