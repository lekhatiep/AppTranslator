namespace AppTranslator.Core.Abstractions;

/// <summary>Ảnh chụp dạng BGRA 32-bit, không phụ thuộc thư viện xử lý ảnh.</summary>
public sealed record Frame(byte[] Pixels, int Width, int Height, int Stride, DateTimeOffset CapturedAt);

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    /// <summary>Hình chữ nhật nhỏ nhất chứa cả hai.</summary>
    public PixelRect Union(PixelRect other)
    {
        int left = Math.Min(X, other.X);
        int top = Math.Min(Y, other.Y);
        int right = Math.Max(X + Width, other.X + other.Width);
        int bottom = Math.Max(Y + Height, other.Y + other.Height);
        return new PixelRect(left, top, right - left, bottom - top);
    }
}
