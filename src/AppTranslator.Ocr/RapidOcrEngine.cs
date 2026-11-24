using System.Runtime.InteropServices;
using AppTranslator.Core.Abstractions;
using OcrResult = AppTranslator.Core.Abstractions.OcrResult;
using RapidOcrNet;
using SkiaSharp;

namespace AppTranslator.Ocr;

/// <summary>
/// PaddleOCR (model PP-OCRv5 Latin, kèm trong gói RapidOcrNet) chạy bằng ONNX Runtime trên CPU.
/// Một instance không dùng song song từ nhiều luồng.
/// </summary>
public sealed class RapidOcrEngine : IOcrEngine, IDisposable
{
    private readonly RapidOcr _ocr = new();

    // Mặc định gói thu cạnh dài về 1024px: vùng phụ đề rộng ~1800px bị thu nửa, chữ chỉ còn ~13px và đọc sai
    // (benchmark Stage 3: 15% → xem implement_plan.md). 4096 đủ cho vùng phụ đề 1080p kể cả khi phóng to 2 lần.
    private const int MaxImageSide = 4096;

    // Chữ phụ đề luôn nằm ngang nên bỏ bước đoán xoay 180°.
    private readonly RapidOcrOptions _options = RapidOcrOptions.Default with { DoAngle = false, ImgResize = MaxImageSide };

    // Tên file model PP-OCRv5 Latin kèm gói RapidOcrNet, được chép vào models/v5 cạnh file chạy.
    private const string ModelDirectory = "models/v5";
    private const string DetectorModel = "ch_PP-OCRv5_mobile_det.onnx";
    private const string ClassifierModel = "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx";
    private const string RecognizerModel = "latin_PP-OCRv5_rec_mobile_infer.onnx";
    private const string CharacterDictionary = "ppocrv5_latin_dict.txt";

    public RapidOcrEngine()
    {
        // Đường dẫn tuyệt đối: InitModels() mặc định tìm theo thư mục hiện hành, sai khi chạy từ thư mục khác.
        string directory = Path.Combine(AppContext.BaseDirectory, ModelDirectory);
        _ocr.InitModels(
            Path.Combine(directory, DetectorModel),
            Path.Combine(directory, ClassifierModel),
            Path.Combine(directory, RecognizerModel),
            Path.Combine(directory, CharacterDictionary));
    }

    public string Name => "PaddleOcrV5";

    public async Task<OcrResult> RecognizeAsync(Frame image, CancellationToken cancellationToken = default)
    {
        using var bitmap = ToBitmap(image);
        var result = await _ocr.DetectAsync(bitmap, _options, null, cancellationToken);

        var lines = new List<OcrLine>();
        foreach (var block in result.TextBlocks)
            lines.Add(new OcrLine(block.Text, GetBounds(block.BoxPoints), GetConfidence(block.CharScores)));

        return new OcrResult(lines);
    }

    public void Dispose()
    {
        _ocr.Dispose();
    }

    private static SKBitmap ToBitmap(Frame image)
    {
        var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        nint destination = bitmap.GetPixels();
        for (int row = 0; row < image.Height; row++)
            Marshal.Copy(image.Pixels, row * image.Stride, destination + row * bitmap.RowBytes, image.Width * 4);

        return bitmap;
    }

    private static PixelRect GetBounds(SKPointI[] corners)
    {
        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = 0;
        int bottom = 0;
        foreach (var corner in corners)
        {
            left = Math.Min(left, corner.X);
            top = Math.Min(top, corner.Y);
            right = Math.Max(right, corner.X);
            bottom = Math.Max(bottom, corner.Y);
        }

        return new PixelRect(left, top, right - left, bottom - top);
    }

    private static double? GetConfidence(float[]? characterScores)
    {
        if (characterScores is null || characterScores.Length == 0)
            return null;

        return characterScores.Average();
    }
}
