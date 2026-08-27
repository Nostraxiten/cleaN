using System.Globalization;
using CleaN.Apps;
using CleaN.Core;

namespace CleaN.ViewModels;

/// <summary>One installed application together with what is known about its last use.</summary>
public sealed class AppUsageItemViewModel : ObservableObject
{
    public AppUsageItemViewModel(AppUsageInfo info)
    {
        Info = info;
    }

    public AppUsageInfo Info { get; }

    public string Name => Info.App.DisplayName;

    public string Publisher => Info.App.Publisher.Length > 0 ? Info.App.Publisher : "Unknown publisher";

    public string Version => Info.App.Version;

    public string InstallLocation => Info.App.InstallLocation;

    public bool HasInstallLocation => InstallLocation.Length > 0 && Directory.Exists(InstallLocation);

    public string SizeText => Info.App.EstimatedSizeBytes > 0
        ? SizeFormatter.Format(Info.App.EstimatedSizeBytes)
        : "Unknown size";

    public string LastUsedText => Info.LastUsed is { } date
        ? date.ToString("d", CultureInfo.CurrentCulture)
        : "Never recorded";

    public string UnusedForText
    {
        get
        {
            if (Info.DaysSinceLastUse is not { } days)
            {
                return "No usage data";
            }

            if (days < 1)
            {
                return "Used today";
            }

            if (days < 60)
            {
                return $"{days} days ago";
            }

            var months = days / 30;
            return months < 24 ? $"{months} months ago" : $"{months / 12} years ago";
        }
    }

    public string EvidenceText => Info.EvidenceDescription;

    public bool CanUninstall => Info.App.CanUninstall;
}
