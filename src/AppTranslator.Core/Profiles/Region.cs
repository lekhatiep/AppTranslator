using AppTranslator.Core.Abstractions;

namespace AppTranslator.Core.Profiles;

public enum RegionType
{
    Subtitle,
    Choices,
}

/// <summary>Vùng chữ, tọa độ theo tỉ lệ 0–1 của cửa sổ nguồn.</summary>
public sealed record Region
{
    public required RegionType Type { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double W { get; init; }
    public double H { get; init; }
    public string? TextColor { get; init; }
    public int? MaxLines { get; init; }
    public string? SelectedMarker { get; init; }

    // 4 chữ số thập phân: sai số dưới 1 pixel ở 4K, file JSON vẫn dễ đọc.
    private const int NormalizedDecimals = 4;

    /// <summary>Đổi vùng sang pixel trên khung hình; phần nằm ngoài khung bị cắt bỏ.</summary>
    public PixelRect ToPixelRect(int frameWidth, int frameHeight)
    {
        int left = ScaleAndClamp(X, frameWidth);
        int top = ScaleAndClamp(Y, frameHeight);
        int right = ScaleAndClamp(X + W, frameWidth);
        int bottom = ScaleAndClamp(Y + H, frameHeight);
        return new PixelRect(left, top, right - left, bottom - top);
    }

    /// <summary>Tạo bản sao của vùng với tọa độ lấy từ hình chữ nhật pixel; giữ nguyên các thuộc tính khác.</summary>
    public Region WithPixelRect(PixelRect rect, int frameWidth, int frameHeight)
    {
        return this with
        {
            X = Normalize(rect.X, frameWidth),
            Y = Normalize(rect.Y, frameHeight),
            W = Normalize(rect.Width, frameWidth),
            H = Normalize(rect.Height, frameHeight),
        };
    }

    private static int ScaleAndClamp(double ratio, int size)
    {
        int pixel = (int)Math.Round(ratio * size);
        return Math.Clamp(pixel, 0, size);
    }

    private static double Normalize(int pixel, int size)
    {
        return Math.Round((double)pixel / size, NormalizedDecimals);
    }
}
