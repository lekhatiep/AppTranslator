using System.Runtime.InteropServices;
using System.Windows.Interop;
using AppTranslator.App.Interop;
using AppTranslator.Core.Settings;

namespace AppTranslator.App;

/// <summary>
/// FR-03: phím tắt toàn cục bằng RegisterHotKey, nhận qua một cửa sổ message-only riêng (không phụ thuộc cửa sổ chính).
/// Phải tạo trên UI thread; hành động chạy trên UI thread.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    // Cửa sổ message-only: không hiển thị, chỉ nhận message.
    private static readonly nint HwndMessage = -3;

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public HotkeyService()
    {
        _source = new HwndSource(new HwndSourceParameters("AppTranslatorHotkeys") { ParentWindow = HwndMessage });
        _source.AddHook(WndProc);
    }

    /// <summary>Đăng ký phím tắt; trả về mã lỗi Win32 nếu thất bại (thường do app khác đã giữ tổ hợp phím đó), 0 nếu thành công.</summary>
    public int Register(HotkeyGesture gesture, Action action)
    {
        int id = _nextId;
        uint modifiers = (uint)gesture.Modifiers | NativeMethods.ModNoRepeat;
        if (!NativeMethods.RegisterHotKey(_source.Handle, id, modifiers, (uint)gesture.VirtualKey))
            return Marshal.GetLastWin32Error();

        _actions[id] = action;
        _nextId++;
        return 0;
    }

    public void Dispose()
    {
        foreach (int id in _actions.Keys)
            NativeMethods.UnregisterHotKey(_source.Handle, id);

        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private nint WndProc(nint windowHandle, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && _actions.TryGetValue((int)wParam, out var action))
        {
            action();
            handled = true;
        }

        return 0;
    }
}
