using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using AppTranslator.App.Interop;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Profiles;

namespace AppTranslator.App.Overlay;

/// <summary>Một khối chữ hiển thị đè lên vùng chữ gốc.</summary>
internal sealed record OverlayText(Region Region, string Text);

/// <summary>Kiểu chữ của overlay, dựng từ OverlaySettings.</summary>
internal sealed record OverlayStyle(double FontSize, Brush Background);

/// <summary>
/// FR-06: cửa sổ trong suốt phủ đúng khung cửa sổ nguồn, không giành focus. Có hai chế độ vì trên Windows 10
/// cửa sổ WPF không thể vừa click-through vừa bị loại khỏi ảnh chụp (đã đo, xem implement_plan.md Stage 2):
/// <list type="bullet">
/// <item>Click-through (dùng với WGC): AllowsTransparency + WS_EX_TRANSPARENT. WGC chỉ chụp cửa sổ nguồn nên overlay không lọt vào ảnh.</item>
/// <item>Loại khỏi ảnh chụp (dùng với DXGI): cửa sổ thường, nền trong suốt nhờ DWM, WDA_EXCLUDEFROMCAPTURE.
/// Không click-through được, nên cắt cửa sổ chỉ còn các ô chữ (SetWindowRgn) và chặn kích hoạt khi click:
/// click trúng ô chữ bị bỏ qua, click chỗ khác vẫn vào game.</item>
/// </list>
/// Vị trí đặt bằng pixel vật lý (SetWindowPos); khối chữ đặt theo tỉ lệ vùng nên không phụ thuộc DPI.
/// </summary>
public partial class OverlayWindow : Window
{
    private const double TextPadding = 6;

    private readonly bool _excludeFromCapture;
    private IReadOnlyList<OverlayText> _texts = [];
    private OverlayStyle? _style;

    public OverlayWindow(bool excludeFromCapture)
    {
        InitializeComponent();
        _excludeFromCapture = excludeFromCapture;
        // Phải đặt trước khi tạo HWND.
        AllowsTransparency = !excludeFromCapture;
        SizeChanged += (_, _) => Relayout();
    }

    public nint Handle { get; private set; }

    /// <summary>Tạo HWND và đặt các cờ Win32; trả về false nếu chế độ DXGI mà Windows không cho loại overlay khỏi ảnh chụp.</summary>
    public bool Initialize()
    {
        Handle = new WindowInteropHelper(this).EnsureHandle();

        long exStyle = NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GwlExStyle);
        exStyle |= NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow;
        if (!_excludeFromCapture)
            exStyle |= NativeMethods.WsExTransparent;
        NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GwlExStyle, (nint)exStyle);

        if (!_excludeFromCapture)
            return true;

        var source = HwndSource.FromHwnd(Handle);
        source.CompositionTarget.BackgroundColor = Colors.Transparent;
        source.AddHook(BlockMouseActivation);

        // Kéo "kính" DWM phủ toàn cửa sổ: phần WPF vẽ trong suốt sẽ nhìn xuyên xuống game.
        var margins = new NativeMethods.Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        NativeMethods.DwmExtendFrameIntoClientArea(Handle, ref margins);

        return NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WdaExcludeFromCapture);
    }

    /// <summary>Đặt overlay trùng khung cửa sổ nguồn (pixel vật lý trên màn hình).</summary>
    public void PlaceOver(PixelRect bounds)
    {
        NativeMethods.SetWindowPos(Handle, NativeMethods.HwndTopmost, bounds.X, bounds.Y, bounds.Width, bounds.Height, NativeMethods.SwpNoActivate);
    }

    internal void SetContent(IReadOnlyList<OverlayText> texts, OverlayStyle style)
    {
        _texts = texts;
        _style = style;
        Relayout();
    }

    private void Relayout()
    {
        RegionCanvas.Children.Clear();
        var boxes = new List<Rect>();

        if (_style is not null && ActualWidth > 0 && ActualHeight > 0)
        {
            foreach (var text in _texts)
            {
                var box = CreateTextBox(text, _style);
                RegionCanvas.Children.Add(box);
                boxes.Add(new Rect(Canvas.GetLeft(box), Canvas.GetTop(box), box.Width, box.Height));
            }
        }

        if (_excludeFromCapture)
            ClipWindowToBoxes(boxes);
    }

    private Border CreateTextBox(OverlayText text, OverlayStyle style)
    {
        var region = text.Region;
        var textBlock = new TextBlock
        {
            Text = text.Text,
            FontSize = style.FontSize,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = region.Type == RegionType.Subtitle ? TextAlignment.Center : TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var border = new Border
        {
            Background = style.Background,
            Padding = new Thickness(TextPadding),
            Width = region.W * ActualWidth,
            Height = region.H * ActualHeight,
            Child = textBlock,
        };
        Canvas.SetLeft(border, region.X * ActualWidth);
        Canvas.SetTop(border, region.Y * ActualHeight);
        return border;
    }

    /// <summary>Chỉ giữ lại phần cửa sổ nằm dưới các ô chữ, để click ngoài ô chữ đi thẳng xuống game.</summary>
    private void ClipWindowToBoxes(IReadOnlyList<Rect> boxes)
    {
        if (Handle == 0)
            return;

        // Ô chữ tính bằng DIP, region của Windows tính bằng pixel vật lý.
        var toDevice = PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice ?? Matrix.Identity;

        nint windowRegion = NativeMethods.CreateRectRgn(0, 0, 0, 0);
        foreach (var box in boxes)
        {
            var devicePoint1 = toDevice.Transform(box.TopLeft);
            var devicePoint2 = toDevice.Transform(box.BottomRight);
            nint boxRegion = NativeMethods.CreateRectRgn(
                (int)Math.Floor(devicePoint1.X), (int)Math.Floor(devicePoint1.Y),
                (int)Math.Ceiling(devicePoint2.X), (int)Math.Ceiling(devicePoint2.Y));
            NativeMethods.CombineRgn(windowRegion, windowRegion, boxRegion, NativeMethods.RgnOr);
            NativeMethods.DeleteObject(boxRegion);
        }

        if (NativeMethods.SetWindowRgn(Handle, windowRegion, true) == 0)
            NativeMethods.DeleteObject(windowRegion);
    }

    private static nint BlockMouseActivation(nint windowHandle, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != NativeMethods.WmMouseActivate)
            return 0;

        handled = true;
        return NativeMethods.MaNoActivate;
    }
}
