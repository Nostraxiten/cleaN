using System.Globalization;

namespace CleaN.Core;

public static class SizeFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    /// <summary>Human readable byte count, e.g. "1.4 GB".</summary>
    public static string Format(long bytes)
    {
        if (bytes < 0)
        {
            return "0 B";
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        var decimals = unit == 0 ? 0 : value < 10 ? 2 : value < 100 ? 1 : 0;
        return value.ToString("N" + decimals, CultureInfo.CurrentCulture) + " " + Units[unit];
    }
}
