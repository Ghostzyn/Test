using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopDrawers.Services;
public static class ShellIcons
{
    public static ImageSource? Load(string path)
    {
        var result = SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x100 | 0x0);
        if (result == 0 || info.hIcon == 0) return null;
        try
        {
            var bitmap = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
            bitmap.Freeze(); return bitmap;
        }
        finally { DestroyIcon(info.hIcon); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public nint hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern nint SHGetFileInfo(string path, uint attributes, out SHFILEINFO info, uint size, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
}
