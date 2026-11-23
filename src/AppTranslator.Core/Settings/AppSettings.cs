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
    public double BackgroundOpacity { get; init; } = 0.6;
}
