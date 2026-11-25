using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using AppTranslator.App.Interop;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Layout;
using AppTranslator.Core.Profiles;

namespace AppTranslator.App.Overlay;

/// <summary>Một khối chữ hiển thị đè lên vùng chữ gốc.</summary>
/// <param name="Anchor">Khung chữ gốc (tỉ lệ cửa sổ) nếu biết; chữ dịch sẽ đè đúng khung này thay vì phủ cả vùng.</param>
/// <param name="HighlightedLine">Dòng (tách bằng \n) cần tô màu nổi bật: lựa chọn đang chọn.</param>
public sealed record OverlayText(Region Region, string Text, LayoutRect? Anchor = null, int? HighlightedLine = null);

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

    // Chỗ chừa bên trái dòng lựa chọn đầu (tính theo cỡ chữ) để che cả ký hiệu ▶ của game.
    private const double ChoiceMarkerSpace = 1.5;

    // Màu lựa chọn đang chọn, gần màu vàng của Witcher 3.
    private static readonly Brush HighlightBrush = CreateFrozenBrush(Color.FromRgb(0xF2, 0xC1, 0x4E));

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

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
        if (text.Region.Type == RegionType.Subtitle)
            return CreateSubtitleBox(text, style);

        return CreateChoicesBox(text, style);
    }

    /// <summary>
    /// Lựa chọn hội thoại (FR-05): mỗi dòng một lựa chọn, dòng đang chọn tô vàng như trong game. Ô bắt đầu ở dòng lựa chọn
    /// đầu tiên (chừa chỗ bên trái để che cả ▶ gốc), kéo tới mép phải vùng, cao theo nội dung (lấn xuống dưới nếu dài).
    /// Chưa biết khung chữ gốc thì phủ cả vùng.
    /// </summary>
    private Border CreateChoicesBox(OverlayText text, OverlayStyle style)
    {
        var region = text.Region;
        double regionLeft = region.X * ActualWidth;
        double regionRight = (region.X + region.W) * ActualWidth;
        double left = regionLeft;
        double top = region.Y * ActualHeight;
        double minHeight = region.H * ActualHeight;
        if (text.Anchor is { } anchor)
        {
            left = Math.Max(regionLeft, anchor.X * ActualWidth - style.FontSize * ChoiceMarkerSpace);
            top = anchor.Y * ActualHeight - TextPadding;
            minHeight = anchor.Height * ActualHeight + 2 * TextPadding;
        }

        var textBlock = new TextBlock
        {
            FontSize = style.FontSize,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
        };
        string[] lines = text.Text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
                textBlock.Inlines.Add(new LineBreak());

            var run = new Run(lines[i]);
            if (i == text.HighlightedLine)
                run.Foreground = HighlightBrush;
            textBlock.Inlines.Add(run);
        }

        double width = Math.Max(1, regionRight - left);
        var border = new Border
        {
            Background = style.Background,
            Padding = new Thickness(TextPadding),
            Width = width,
            Child = textBlock,
        };
        // Đo để biết chiều cao thật (cần cho vùng cắt cửa sổ ở chế độ DXGI).
        border.Measure(new Size(width, double.PositiveInfinity));
        border.Height = Math.Max(minHeight, border.DesiredSize.Height);
        Canvas.SetLeft(border, left);
        Canvas.SetTop(border, top);
        return border;
    }

    /// <summary>
    /// Phụ đề: ô vừa với bản dịch, phủ khung chữ gốc, mở rộng lên trên và co chữ khi dài (<see cref="SubtitleLayout"/>).
    /// Chưa biết khung chữ gốc (vd thông báo lỗi) thì neo vào đáy giữa vùng phụ đề.
    /// </summary>
    private Border CreateSubtitleBox(OverlayText text, OverlayStyle style)
    {
        var region = text.Region;
        var area = new LayoutRect(region.X * ActualWidth, region.Y * ActualHeight, region.W * ActualWidth, region.H * ActualHeight);
        var anchor = text.Anchor is { } ratio
            ? new LayoutRect(ratio.X * ActualWidth, ratio.Y * ActualHeight, ratio.Width * ActualWidth, ratio.Height * ActualHeight)
            : new LayoutRect(area.X + area.Width / 2, area.Bottom - TextPadding, 0, 0);

        var textBlock = new TextBlock
        {
            Text = text.Text,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var typeface = new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch);
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        (double Width, double Height) Measure(double fontSize, double maxWidth)
        {
            var formatted = new FormattedText(text.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface,
                fontSize, Brushes.White, pixelsPerDip)
            {
                MaxTextWidth = maxWidth,
                TextAlignment = TextAlignment.Center,
            };
            // +1: tránh TextBlock xuống dòng khác FormattedText vì làm tròn.
            return (formatted.Width + 1, formatted.Height);
        }

        var layout = SubtitleLayout.Compute(anchor, area, style.FontSize, TextPadding, Measure);
        textBlock.FontSize = layout.FontSize;

        var border = new Border
        {
            Background = style.Background,
            Padding = new Thickness(TextPadding),
            Width = layout.Box.Width,
            Height = layout.Box.Height,
            Child = textBlock,
        };
        Canvas.SetLeft(border, layout.Box.X);
        Canvas.SetTop(border, layout.Box.Y);
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
