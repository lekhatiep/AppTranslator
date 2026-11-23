using AppTranslator.App.Interop;

namespace AppTranslator.App.Overlay;

/// <summary>
/// Báo khi cửa sổ nguồn di chuyển, đổi kích thước, thu nhỏ/khôi phục, bị đóng, hoặc khi cửa sổ đang dùng (foreground) thay đổi.
/// Hook chạy kiểu out-of-context nên callback đến trên chính UI thread đã tạo hook.
/// </summary>
internal sealed class SourceWindowTracker : IDisposable
{
    // Giữ delegate làm field để GC không thu hồi khi Windows vẫn đang gọi.
    private readonly NativeMethods.WinEventProc _callback;
    private readonly nint _systemHook;
    private nint _locationHook;

    public SourceWindowTracker()
    {
        _callback = OnWinEvent;
        // Foreground và thu nhỏ/khôi phục: theo dõi mọi tiến trình (ít sự kiện).
        _systemHook = NativeMethods.SetWinEventHook(
            NativeMethods.EventSystemForeground, NativeMethods.EventSystemMinimizeEnd,
            0, _callback, 0, 0, NativeMethods.WinEventOutOfContext);
    }

    public event Action? Changed;

    public nint Target { get; private set; }

    public void SetTarget(nint windowHandle)
    {
        UnhookLocation();
        Target = windowHandle;

        if (windowHandle != 0)
        {
            // Sự kiện vị trí rất nhiều (cả con trỏ chuột), nên chỉ nghe tiến trình của cửa sổ nguồn.
            NativeMethods.GetWindowThreadProcessId(windowHandle, out uint processId);
            _locationHook = NativeMethods.SetWinEventHook(
                NativeMethods.EventObjectDestroy, NativeMethods.EventObjectLocationChange,
                0, _callback, processId, 0, NativeMethods.WinEventOutOfContext);
        }

        Changed?.Invoke();
    }

    /// <summary>Cửa sổ nguồn có đang là cửa sổ người dùng đang dùng không (kể cả popup của nó).</summary>
    public bool IsTargetForeground()
    {
        if (Target == 0)
            return false;

        nint foreground = NativeMethods.GetForegroundWindow();
        return foreground == Target || NativeMethods.GetAncestor(foreground, NativeMethods.GaRootOwner) == Target;
    }

    public bool IsTargetMinimized()
    {
        return Target != 0 && NativeMethods.IsIconic(Target);
    }

    public void Dispose()
    {
        UnhookLocation();
        NativeMethods.UnhookWinEvent(_systemHook);
    }

    private void UnhookLocation()
    {
        if (_locationHook == 0)
            return;

        NativeMethods.UnhookWinEvent(_locationHook);
        _locationHook = 0;
    }

    private void OnWinEvent(nint hook, uint eventType, nint windowHandle, int objectId, int childId, uint threadId, uint time)
    {
        if (eventType == NativeMethods.EventSystemForeground)
        {
            Changed?.Invoke();
            return;
        }

        // Còn lại chỉ quan tâm đến chính cửa sổ nguồn (bỏ qua control con, con trỏ...).
        if (windowHandle != Target || objectId != NativeMethods.ObjectIdWindow)
            return;

        bool isRelevant = eventType == NativeMethods.EventObjectLocationChange
            || eventType == NativeMethods.EventObjectDestroy
            || eventType == NativeMethods.EventSystemMinimizeStart
            || eventType == NativeMethods.EventSystemMinimizeEnd;
        if (isRelevant)
            Changed?.Invoke();
    }
}
