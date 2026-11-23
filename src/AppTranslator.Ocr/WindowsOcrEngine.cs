using System.Runtime.InteropServices.WindowsRuntime;
using AppTranslator.Core.Abstractions;
using AppTranslator.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using WinRtOcrEngine = Windows.Media.Ocr.OcrEngine;

namespace AppTranslator.Ocr;

/// <summary>OCR có sẵn trong Windows (Windows.Media.Ocr). Cần gói ngôn ngữ tiếng Anh có thành phần OCR.</summary>
public sealed class WindowsOcrEngine : IOcrEngine
{
    private const string LanguageTag = "en-US";

    private readonly WinRtOcrEngine _engine;

    public WindowsOcrEngine()
    {
        _engine = WinRtOcrEngine.TryCreateFromLanguage(new Language(LanguageTag))
            ?? throw new InvalidOperationException(
                $"Windows chưa cài OCR cho {LanguageTag}. Vào Settings > Time & Language > Language, thêm English (United States) có Optical character recognition.");
    }

    public string Name => "WindowsOcr";

    public async Task<OcrResult> RecognizeAsync(Frame image, CancellationToken cancellationToken = default)
    {
        // Windows OCR từ chối ảnh có cạnh dài hơn MaxImageDimension (2600px), vd vùng phụ đề phóng to 2 lần.
        var fitted = FrameConverter.ScaleDownToFit(image, (int)WinRtOcrEngine.MaxImageDimension);
        double scaleBack = (double)image.Width / fitted.Width;

        using var bitmap = SoftwareBitmap.CreateCopyFromBuffer(
            fitted.Pixels.AsBuffer(), BitmapPixelFormat.Bgra8, fitted.Width, fitted.Height, BitmapAlphaMode.Ignore);
        var result = await _engine.RecognizeAsync(bitmap).AsTask(cancellationToken);

        var lines = new List<OcrLine>();
        foreach (var line in result.Lines)
            lines.Add(new OcrLine(line.Text, GetBounds(line, scaleBack), Confidence: null));

        return new OcrResult(lines);
    }

    private static PixelRect GetBounds(Windows.Media.Ocr.OcrLine line, double scaleBack)
    {
        double left = double.MaxValue;
        double top = double.MaxValue;
        double right = 0;
        double bottom = 0;
        foreach (var word in line.Words)
        {
            left = Math.Min(left, word.BoundingRect.Left);
            top = Math.Min(top, word.BoundingRect.Top);
            right = Math.Max(right, word.BoundingRect.Right);
            bottom = Math.Max(bottom, word.BoundingRect.Bottom);
        }

        return new PixelRect(
            (int)(left * scaleBack), (int)(top * scaleBack),
            (int)((right - left) * scaleBack), (int)((bottom - top) * scaleBack));
    }
}
