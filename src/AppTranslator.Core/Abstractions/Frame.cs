namespace AppTranslator.Core.Abstractions;

/// <summary>Ảnh chụp dạng BGRA 32-bit, không phụ thuộc thư viện xử lý ảnh.</summary>
public sealed record Frame(byte[] Pixels, int Width, int Height, int Stride, DateTimeOffset CapturedAt);

public readonly record struct PixelRect(int X, int Y, int Width, int Height);
