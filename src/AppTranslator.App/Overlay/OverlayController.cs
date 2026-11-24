using System.Windows.Media;
using AppTranslator.Capture.Windows;
using AppTranslator.Core.Layout;
using AppTranslator.Core.Profiles;
using AppTranslator.Core.Settings;
using Microsoft.Extensions.Logging;

namespace AppTranslator.App.Overlay;

/// <summary>
/// Quyết định khi nào overlay hiện và ở đâu: chỉ hiện khi đang bật dịch, cửa sổ nguồn còn sống, không thu nhỏ
/// và đang là cửa sổ người dùng dùng (để Alt-Tab sang app khác thì chữ không nổi đè lên app đó).
/// Chọn chế độ overlay theo engine chụp (xem <see cref="OverlayWindow"/>). Phải tạo trên UI thread.
/// </summary>
public sealed class OverlayController : IDisposable
{
    private readonly SourceWindowTracker _tracker = new();
    private readonly ILogger<OverlayController> _logger;
    // Chỉ tạo khi biết engine (SetCaptureEngine), để lúc khởi động không phải đóng cửa sổ nào:
    // WPF coi cửa sổ tạo đầu tiên là MainWindow, đóng nó sẽ tắt app.
    private OverlayWindow? _window;
    private bool _isEnabled;
    private IReadOnlyList<OverlayText> _texts = [];
    private OverlayStyle? _style;

    public OverlayController(ILogger<OverlayController> logger)
    {
        _logger = logger;
        _tracker.Changed += Refresh;
    }

    public CaptureEngine CaptureEngine { get; private set; }

    /// <summary>Đổi chế độ overlay cho hợp với engine chụp: DXGI cần overlay không lọt vào ảnh chụp.</summary>
    public void SetCaptureEngine(CaptureEngine engine)
    {
        if (_window is not null && engine == CaptureEngine)
            return;

        _window?.Close();
        _window = CreateWindow(engine);
        CaptureEngine = engine;
        if (_style is not null)
            _window.SetContent(_texts, _style);

        Refresh();
    }

    public void SetEnabled(bool isEnabled)
    {
        _isEnabled = isEnabled;
        Refresh();
    }

    public void SetTarget(nint windowHandle)
    {
        _tracker.SetTarget(windowHandle);
    }


    /// <param name="texts">Anchor: khung chữ gốc theo tỉ lệ cửa sổ (null nếu không biết).</param>
    public void ShowTexts(IReadOnlyList<(Region Region, string Text, LayoutRect? Anchor)> texts, OverlaySettings settings)
    {
        var overlayTexts = new List<OverlayText>();
        foreach (var (region, text, anchor) in texts)
            overlayTexts.Add(new OverlayText(region, text, anchor));

        _texts = overlayTexts;
        _style = new OverlayStyle(settings.FontSize, CreateBackground(settings));
        _window?.SetContent(_texts, _style);
    }

    public void Dispose()
    {
        _tracker.Changed -= Refresh;
        _tracker.Dispose();
        _window?.Close();
    }

    private OverlayWindow CreateWindow(CaptureEngine engine)
    {
        bool excludeFromCapture = engine == CaptureEngine.DesktopDuplication;
        var window = new OverlayWindow(excludeFromCapture);
        if (!window.Initialize())
            _logger.LogWarning("Overlay: không đặt được WDA_EXCLUDEFROMCAPTURE, overlay sẽ lọt vào ảnh chụp DXGI");

        _logger.LogInformation("Overlay: chế độ {Mode}", excludeFromCapture ? "loại khỏi ảnh chụp (DXGI)" : "click-through (WGC)");
        return window;
    }

    private void Refresh()
    {
        if (_window is null)
            return;

        nint target = _tracker.Target;
        bool canShow = _isEnabled && target != 0 && WindowFinder.IsAlive(target)
            && !_tracker.IsTargetMinimized() && _tracker.IsTargetForeground();

        var bounds = canShow ? WindowFinder.GetBounds(target) : null;
        if (bounds is null)
        {
            _window.Hide();
            return;
        }

        if (!_window.IsVisible)
            _window.Show();

        _window.PlaceOver(bounds.Value);
    }

    private Brush CreateBackground(OverlaySettings settings)
    {
        Color color;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(settings.BackgroundColor);
        }
        catch (FormatException)
        {
            // Màu do người dùng tự sửa trong settings.json; sai thì dùng nền đen thay vì làm hỏng overlay.
            _logger.LogWarning("Overlay: màu nền {Color} không hợp lệ, dùng màu đen", settings.BackgroundColor);
            color = Colors.Black;
        }

        color.A = (byte)Math.Round(Math.Clamp(settings.BackgroundOpacity, 0, 1) * byte.MaxValue);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
