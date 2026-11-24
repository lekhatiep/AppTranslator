namespace AppTranslator.Core.Layout;

/// <summary>Hình chữ nhật số thực: tỉ lệ 0–1 của cửa sổ, hoặc đơn vị vẽ (DIP) tùy chỗ dùng.</summary>
public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

/// <param name="Box">Ô nền chữ dịch (đã gồm lề).</param>
/// <param name="FontSize">Cỡ chữ sau khi co.</param>
public sealed record SubtitleLayoutResult(LayoutRect Box, double FontSize);

/// <summary>
/// FR-06: đặt bản dịch phụ đề đè đúng chỗ chữ gốc. Ô chữ luôn phủ hết khung chữ gốc (để chữ tiếng Anh không lộ ra),
/// căn giữa theo chữ gốc, giữ cạnh dưới; bản dịch dài thì xuống dòng và mở rộng lên trên, tối đa tới mép trên vùng phụ đề.
/// Vẫn không đủ chỗ thì co chữ, nhỏ nhất <see cref="MinFontScale"/> cỡ chữ cài đặt (nhỏ hơn khó đọc trên TV);
/// co hết mức vẫn tràn thì cho vượt lên trên vùng.
/// </summary>
public static class SubtitleLayout
{
    public const double MinFontScale = 0.6;
    private const double MinWrapWidthRatio = 0.5;
    private const double FontStep = 1;

    /// <param name="anchor">Khung chữ gốc.</param>
    /// <param name="area">Vùng phụ đề trong hồ sơ: giới hạn bề rộng và khoảng được mở rộng lên trên.</param>
    /// <param name="measure">Đo khối chữ (cỡ chữ, bề rộng tối đa trước khi xuống dòng) → (rộng, cao) thực tế.</param>
    public static SubtitleLayoutResult Compute(
        LayoutRect anchor,
        LayoutRect area,
        double preferredFontSize,
        double padding,
        Func<double, double, (double Width, double Height)> measure)
    {
        // Xuống dòng theo bề rộng chữ gốc (phụ đề game đã tự xuống dòng ở bề rộng dễ đọc), tối thiểu nửa vùng để câu
        // gốc ngắn mà bản dịch dài không bị bẻ thành quá nhiều dòng hẹp.
        double wrapWidth = Math.Min(area.Width, Math.Max(anchor.Width + 2 * padding, area.Width * MinWrapWidthRatio));
        double maxTextWidth = Math.Max(1, wrapWidth - 2 * padding);
        double bottom = anchor.Bottom + padding;
        double maxBoxHeight = bottom - area.Y;
        double minFontSize = preferredFontSize * MinFontScale;

        double fontSize = preferredFontSize;
        var text = measure(fontSize, maxTextWidth);
        while (text.Height + 2 * padding > maxBoxHeight && fontSize > minFontSize)
        {
            fontSize = Math.Max(minFontSize, fontSize - FontStep);
            text = measure(fontSize, maxTextWidth);
        }

        double boxWidth = Math.Min(area.Width, Math.Max(text.Width, anchor.Width) + 2 * padding);
        double boxHeight = Math.Max(text.Height, anchor.Height) + 2 * padding;

        double x = anchor.X + anchor.Width / 2 - boxWidth / 2;
        x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - boxWidth));
        double y = Math.Max(0, bottom - boxHeight);

        return new SubtitleLayoutResult(new LayoutRect(x, y, boxWidth, boxHeight), fontSize);
    }
}
