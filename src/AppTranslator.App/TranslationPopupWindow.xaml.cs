using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AppTranslator.App.Interop;
using AppTranslator.Core.Abstractions;

namespace AppTranslator.App;

/// <summary>
/// FR-07: hiện bản dịch của vùng khoanh thủ công, ngay dưới vùng đó (thiếu chỗ thì phía trên), trong khung cửa sổ game.
/// Không giành focus (tay cầm vẫn điều khiển game) và bị loại khỏi ảnh chụp (không lọt vào OCR). Click vào để đóng.
/// </summary>
public partial class TranslationPopupWindow : Window
{
    private const int GapToArea = 8;

    private readonly PixelRect _area;
    private readonly PixelRect _gameBounds;

    /// <param name="area">Vùng đã khoanh trên màn hình (pixel vật lý).</param>
    /// <param name="gameBounds">Khung cửa sổ game trên màn hình (pixel vật lý).</param>
    public TranslationPopupWindow(PixelRect area, PixelRect gameBounds)
    {
        InitializeComponent();
        _area = area;
        _gameBounds = gameBounds;
        SourceInitialized += (_, _) => ApplyWindowFlags();
        SizeChanged += (_, _) => PlaceNearArea();
    }

    public void SetText(string translation, string? source)
    {
        TranslationText.Text = translation;
        SourceText.Text = source ?? string.Empty;
        SourceText.Visibility = string.IsNullOrEmpty(source) ? Visibility.Collapsed : Visibility.Visible;
    }

    private nint Handle => new WindowInteropHelper(this).Handle;

    private void ApplyWindowFlags()
    {
        long exStyle = NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GwlExStyle);
        exStyle |= NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow;
        NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GwlExStyle, (nint)exStyle);
        // Cửa sổ thường (không layered) nên loại khỏi ảnh chụp được, kể cả khi chụp bằng DXGI.
        NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WdaExcludeFromCapture);
    }

    /// <summary>Đặt popup dưới vùng (thiếu chỗ thì trên), canh trái theo vùng, kẹp trong khung cửa sổ game.</summary>
    private void PlaceNearArea()
    {
        if (Handle == 0)
            return;

        var dpi = VisualTreeHelper.GetDpi(this);
        int width = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
        int height = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);

        int gameRight = _gameBounds.X + _gameBounds.Width;
        int gameBottom = _gameBounds.Y + _gameBounds.Height;
        int x = Math.Clamp(_area.X, _gameBounds.X, Math.Max(_gameBounds.X, gameRight - width));
        int below = _area.Y + _area.Height + GapToArea;
        int y = below + height <= gameBottom ? below : _area.Y - GapToArea - height;
        y = Math.Clamp(y, _gameBounds.Y, Math.Max(_gameBounds.Y, gameBottom - height));

        NativeMethods.SetWindowPos(Handle, NativeMethods.HwndTopmost, x, y, 0, 0, NativeMethods.SwpNoActivate | NativeMethods.SwpNoSize);
    }

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        Close();
    }
}
