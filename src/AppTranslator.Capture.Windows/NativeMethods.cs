using System.Runtime.InteropServices;
using System.Text;

namespace AppTranslator.Capture.Windows;

internal static class NativeMethods
{
    public delegate bool EnumWindowsProc(nint windowHandle, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public const int GwlExStyle = -20;
    public const long WsExToolWindow = 0x00000080;
    public const int DwmwaExtendedFrameBounds = 9;
    public const int DwmwaCloaked = 14;
    public const uint MonitorDefaultToNearest = 2;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(nint windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(nint windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(nint windowHandle, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport("user32.dll")]
    public static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(nint windowHandle, int attribute, out Rect value, int size);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(nint windowHandle, int attribute, out int value, int size);

    [DllImport("d3d11.dll", ExactSpelling = true)]
    public static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);

    public static string GetWindowTitle(nint windowHandle)
    {
        int length = GetWindowTextLength(windowHandle);
        if (length == 0)
            return string.Empty;

        var builder = new StringBuilder(length + 1);
        GetWindowText(windowHandle, builder, builder.Capacity);
        return builder.ToString();
    }

    /// <summary>Khung cửa sổ thật (không gồm bóng đổ), tính bằng pixel vật lý trên màn hình.</summary>
    public static Rect? GetFrameBounds(nint windowHandle)
    {
        int result = DwmGetWindowAttribute(windowHandle, DwmwaExtendedFrameBounds, out Rect bounds, Marshal.SizeOf<Rect>());
        if (result != 0)
            return null;

        return bounds;
    }

    public static bool IsCloaked(nint windowHandle)
    {
        int result = DwmGetWindowAttribute(windowHandle, DwmwaCloaked, out int cloaked, sizeof(int));
        return result == 0 && cloaked != 0;
    }
}
