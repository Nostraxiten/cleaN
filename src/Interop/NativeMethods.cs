using System.Runtime.InteropServices;

namespace CleaN.Interop;

/// <summary>Thin P/Invoke layer. Everything here is Windows-only by definition.</summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    internal const uint SHERB_NOCONFIRMATION = 0x00000001;
    internal const uint SHERB_NOPROGRESSUI = 0x00000002;
    internal const uint SHERB_NOSOUND = 0x00000004;

    internal const int S_OK = 0;

    /// <summary>Reports the size and item count of the recycle bin for a drive (null = all drives).</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    internal static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    /// <summary>Empties the recycle bin for a drive (null = all drives).</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    internal static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
}
