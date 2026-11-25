using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Pipeline;
using AppTranslator.Ocr;

namespace AppTranslator.App.Pipeline;

/// <summary>
/// FR-07: đọc chữ trong vùng khoanh thủ công bằng PaddleOCR (chữ đa dạng hơn phụ đề; Paddle đọc tốt hơn ở benchmark lựa chọn).
/// Giữ mọi dòng, nối trên xuống.
/// </summary>
public sealed class AreaTextReader
{
    private RapidOcrEngine? _ocr;

    public async Task<string> ReadAsync(Frame crop, CancellationToken cancellationToken = default)
    {
        // Nạp model (~1 s) và OCR đều tốn CPU: chạy nền để không đứng giao diện.
        _ocr ??= await Task.Run(() => new RapidOcrEngine(), cancellationToken);
        var ocr = _ocr;
        var result = await Task.Run(() => ocr.RecognizeAsync(crop, cancellationToken), cancellationToken);
        return SentenceAssembler.AssembleAllRows(result.Lines).Text;
    }
}
