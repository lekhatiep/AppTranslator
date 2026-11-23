using System.Diagnostics;
using AppTranslator.Core.Abstractions;

namespace AppTranslator.Capture.Windows;

public sealed record WindowInfo(nint Handle, string Title, string ProcessName)
{
    public string DisplayName => $"{Title}  ({ProcessName})";
}

/// <summary>Liệt kê các cửa sổ người dùng có thể chọn làm nguồn chụp (FR-01).</summary>
public static class WindowFinder
{
    public static IReadOnlyList<WindowInfo> GetCapturableWindows()
    {
        int ownProcessId = Environment.ProcessId;
        var windows = new List<WindowInfo>();

        NativeMethods.EnumWindows((handle, _) =>
        {
            if (IsCapturable(handle, ownProcessId))
            {
                NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
                windows.Add(new WindowInfo(handle, NativeMethods.GetWindowTitle(handle), GetProcessName(processId)));
            }

            return true;
        }, 0);

        return windows;
    }

    /// <summary>Tìm cửa sổ đầu tiên có tiêu đề chứa gợi ý của hồ sơ game (không phân biệt hoa thường).</summary>
    public static WindowInfo? FindByTitleHint(IReadOnlyList<WindowInfo> windows, string? titleHint)
    {
        if (string.IsNullOrWhiteSpace(titleHint))
            return null;

        foreach (var window in windows)
        {
            if (window.Title.Contains(titleHint, StringComparison.OrdinalIgnoreCase))
                return window;
        }

        return null;
    }

    public static bool IsAlive(nint windowHandle)
    {
        return NativeMethods.IsWindow(windowHandle);
    }

    /// <summary>Khung cửa sổ trên màn hình, pixel vật lý; đúng phần mà cả hai engine chụp. Null nếu không lấy được.</summary>
    public static PixelRect? GetBounds(nint windowHandle)
    {
        var bounds = NativeMethods.GetFrameBounds(windowHandle);
        if (bounds is null)
            return null;

        var rect = bounds.Value;
        return new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    private static bool IsCapturable(nint handle, int ownProcessId)
    {
        if (!NativeMethods.IsWindowVisible(handle) || NativeMethods.IsCloaked(handle))
            return false;

        if (NativeMethods.GetWindowTextLength(handle) == 0)
            return false;

        long exStyle = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle);
        if ((exStyle & NativeMethods.WsExToolWindow) != 0)
            return false;

        NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
        if (processId == ownProcessId)
            return false;

        var bounds = NativeMethods.GetFrameBounds(handle);
        return bounds is not null && bounds.Value.Right > bounds.Value.Left && bounds.Value.Bottom > bounds.Value.Top;
    }

    private static string GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            // Tiến trình vừa thoát giữa lúc liệt kê; vẫn hiện cửa sổ, chỉ thiếu tên tiến trình.
            return "?";
        }
    }
}
