using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using AppTranslator.App.Interop;
using AppTranslator.Core.Abstractions;

namespace AppTranslator.App;

/// <summary>
/// FR-07: phủ ảnh chụp đứng yên lên đúng khung cửa sổ game để người chơi kéo chuột khoanh vùng cần dịch.
/// Kết quả tính theo pixel của ảnh chụp, không phụ thuộc DPI.
/// </summary>
public partial class AreaPickerWindow : Window
{
    // Vùng nhỏ hơn mức này (pixel ảnh) coi như bấm nhầm.
    private const int MinimumAreaSize = 8;

    private readonly PixelRect _screenBounds;
    private readonly int _imageWidth;
    private readonly int _imageHeight;
    private Point? _dragStart;

    /// <param name="screenBounds">Khung cửa sổ game trên màn hình (pixel vật lý), để phủ đúng chỗ.</param>
    public AreaPickerWindow(BitmapSource screenshot, PixelRect screenBounds)
    {
        InitializeComponent();
        _screenBounds = screenBounds;
        _imageWidth = screenshot.PixelWidth;
        _imageHeight = screenshot.PixelHeight;
        ScreenshotImage.Source = screenshot;
        SourceInitialized += (_, _) => PlaceOverGameWindow();
    }

    /// <summary>Vùng đã khoanh (pixel ảnh chụp); chỉ có giá trị khi ShowDialog trả về true.</summary>
    public PixelRect? Result { get; private set; }

    private void PlaceOverGameWindow()
    {
        var handle = new WindowInteropHelper(this).Handle;
        NativeMethods.SetWindowPos(handle, NativeMethods.HwndTopmost,
            _screenBounds.X, _screenBounds.Y, _screenBounds.Width, _screenBounds.Height, 0);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            DialogResult = false;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(SelectionCanvas);
        SelectionCanvas.CaptureMouse();
        UpdateSelectionBox(_dragStart.Value, _dragStart.Value);
        SelectionBox.Visibility = Visibility.Visible;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not null)
            UpdateSelectionBox(_dragStart.Value, e.GetPosition(SelectionCanvas));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null)
            return;

        SelectionCanvas.ReleaseMouseCapture();
        var area = ToImagePixels(_dragStart.Value, e.GetPosition(SelectionCanvas));
        _dragStart = null;

        if (area.Width < MinimumAreaSize || area.Height < MinimumAreaSize)
        {
            SelectionBox.Visibility = Visibility.Collapsed;
            return;
        }

        Result = area;
        DialogResult = true;
    }

    private void UpdateSelectionBox(Point start, Point end)
    {
        Canvas.SetLeft(SelectionBox, Math.Min(start.X, end.X));
        Canvas.SetTop(SelectionBox, Math.Min(start.Y, end.Y));
        SelectionBox.Width = Math.Abs(end.X - start.X);
        SelectionBox.Height = Math.Abs(end.Y - start.Y);
    }

    /// <summary>Đổi hai điểm trên cửa sổ (DIP) sang hình chữ nhật trong ảnh chụp (pixel), kẹp trong ảnh.</summary>
    private PixelRect ToImagePixels(Point start, Point end)
    {
        double scaleX = _imageWidth / Math.Max(1, SelectionCanvas.ActualWidth);
        double scaleY = _imageHeight / Math.Max(1, SelectionCanvas.ActualHeight);
        int left = Math.Clamp((int)(Math.Min(start.X, end.X) * scaleX), 0, _imageWidth);
        int top = Math.Clamp((int)(Math.Min(start.Y, end.Y) * scaleY), 0, _imageHeight);
        int right = Math.Clamp((int)Math.Ceiling(Math.Max(start.X, end.X) * scaleX), 0, _imageWidth);
        int bottom = Math.Clamp((int)Math.Ceiling(Math.Max(start.Y, end.Y) * scaleY), 0, _imageHeight);
        return new PixelRect(left, top, right - left, bottom - top);
    }
}
