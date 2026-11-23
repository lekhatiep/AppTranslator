namespace AppTranslator.Core.Abstractions;

public sealed record OcrLine(string Text, PixelRect Bounds, double? Confidence);

public sealed record OcrResult(IReadOnlyList<OcrLine> Lines);

public interface IOcrEngine
{
    string Name { get; }

    Task<OcrResult> RecognizeAsync(Frame image, CancellationToken cancellationToken = default);
}
