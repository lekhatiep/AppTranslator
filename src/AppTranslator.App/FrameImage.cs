using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AppTranslator.Core.Abstractions;

namespace AppTranslator.App;

/// <summary>Đổi <see cref="Frame"/> sang ảnh WPF để hiển thị và lưu PNG.</summary>
internal static class FrameImage
{
    private const double ScreenDpi = 96;

    public static BitmapSource ToBitmapSource(Frame frame)
    {
        // Bgr32 bỏ qua byte alpha: ảnh của Desktop Duplication có alpha không xác định, hiển thị Bgra32 sẽ bị trong suốt.
        var bitmap = BitmapSource.Create(frame.Width, frame.Height, ScreenDpi, ScreenDpi, PixelFormats.Bgr32, null, frame.Pixels, frame.Stride);
        bitmap.Freeze();
        return bitmap;
    }

    public static BitmapSource Crop(BitmapSource source, PixelRect area)
    {
        var cropped = new CroppedBitmap(source, new Int32Rect(area.X, area.Y, area.Width, area.Height));
        cropped.Freeze();
        return cropped;
    }

    public static void SavePng(BitmapSource image, string filePath)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }
}
