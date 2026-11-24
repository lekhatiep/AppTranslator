namespace AppTranslator.Core.Settings;

public enum CaptureEngine
{
    WindowsGraphicsCapture,
    DesktopDuplication,
}

public sealed record AppSettings
{
    public string ActiveProfileId { get; init; } = "default";
    public CaptureEngine CaptureEngine { get; init; } = CaptureEngine.WindowsGraphicsCapture;
    public string TranslatorProvider { get; init; } = "Mock";
    // Tạm chọn theo tài liệu Gemini 11/2026 (Flash-Lite: nhanh, rẻ); chốt lại khi có key và đo được độ trễ.
    public string GeminiModel { get; init; } = "gemini-3.5-flash-lite";
    public HotkeySettings Hotkeys { get; init; } = new();
    public OverlaySettings Overlay { get; init; } = new();
}

public sealed record HotkeySettings
{
    public string ToggleTranslation { get; init; } = "Ctrl+Alt+T";
    public string SelectRegion { get; init; } = "Ctrl+Alt+R";
}

public sealed record OverlaySettings
{
    public double FontSize { get; init; } = 28;
    public string BackgroundColor { get; init; } = "#000000";
    // 0.9: thấp hơn thì phụ đề tiếng Anh gốc lộ qua nền và chồng lên chữ dịch (đã thử trên video Witcher 3).
    public double BackgroundOpacity { get; init; } = 0.9;
}
