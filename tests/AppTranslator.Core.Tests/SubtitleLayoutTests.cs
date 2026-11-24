using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Layout;
using AppTranslator.Core.Pipeline;

namespace AppTranslator.Core.Tests;

public class SubtitleLayoutTests
{
    // Vùng phụ đề 1080p kiểu hồ sơ Witcher 3: x 40–1900, y 800–1050. Chữ gốc một dòng ở giữa dưới, đáy y 1000.
    private static readonly LayoutRect Area = new(40, 800, 1860, 250);
    private static readonly LayoutRect OneLineAnchor = new(700, 970, 520, 30);
    private const double Padding = 6;

    [Fact]
    public void ShortTranslation_CoversOriginalText_CenteredAndSameBottom()
    {
        var result = SubtitleLayout.Compute(OneLineAnchor, Area, 28, Padding, FakeMeasure(10));

        Assert.Equal(28, result.FontSize);
        // Chữ dịch ngắn hơn chữ gốc: ô vẫn phủ hết chữ gốc.
        Assert.True(result.Box.X <= OneLineAnchor.X);
        Assert.True(result.Box.Right >= OneLineAnchor.Right);
        Assert.True(result.Box.Y <= OneLineAnchor.Y);
        Assert.Equal(OneLineAnchor.Bottom + Padding, result.Box.Bottom, precision: 6);
        Assert.Equal(OneLineAnchor.X + OneLineAnchor.Width / 2, result.Box.X + result.Box.Width / 2, precision: 6);
    }

    [Fact]
    public void LongTranslation_WrapsAndGrowsUpward_KeepingBottom()
    {
        // 300 ký tự, cỡ 28 → ~14 px/ký tự → rộng 4200 px, phải xuống 3 dòng trong 1848 px.
        var result = SubtitleLayout.Compute(OneLineAnchor, Area, 28, Padding, FakeMeasure(300));

        Assert.True(result.Box.Height > OneLineAnchor.Height + 2 * Padding);
        Assert.Equal(OneLineAnchor.Bottom + Padding, result.Box.Bottom, precision: 6);
        Assert.True(result.Box.Y >= Area.Y);
        Assert.True(result.Box.Width <= Area.Width);
    }

    [Fact]
    public void WideOriginal_WrapsAtOriginalWidth()
    {
        // Chữ gốc rộng 1600 px: bản dịch dài xuống dòng ở ~1600 px chứ không kéo hết vùng 1860 px.
        var anchor = new LayoutRect(160, 800 + 120, 1600, 60);
        var result = SubtitleLayout.Compute(anchor, Area, 28, Padding, FakeMeasure(300));

        Assert.Equal(anchor.Width + 2 * Padding, result.Box.Width, precision: 6);
    }

    [Fact]
    public void NarrowOriginal_WrapsAtHalfArea()
    {
        var result = SubtitleLayout.Compute(OneLineAnchor, Area, 28, Padding, FakeMeasure(300));

        Assert.Equal(Area.Width / 2, result.Box.Width, precision: 6);
    }

    [Fact]
    public void NotEnoughHeight_ShrinksFont_NotBelowMinimum()
    {
        // Chữ gốc sát mép trên vùng: chỉ còn ~66 px để mở rộng → phải co chữ.
        var anchor = new LayoutRect(700, 806, 520, 30);
        var result = SubtitleLayout.Compute(anchor, Area, 28, Padding, FakeMeasure(300));

        Assert.True(result.FontSize < 28);
        Assert.True(result.FontSize >= 28 * SubtitleLayout.MinFontScale);
    }

    [Fact]
    public void HugeTranslation_MinimumFont_AllowedAboveArea()
    {
        var result = SubtitleLayout.Compute(OneLineAnchor, Area, 28, Padding, FakeMeasure(2000));

        Assert.Equal(28 * SubtitleLayout.MinFontScale, result.FontSize, precision: 6);
        Assert.True(result.Box.Y < Area.Y);
        Assert.True(result.Box.Y >= 0);
    }

    [Fact]
    public void AnchorNearEdge_BoxStaysInsideArea()
    {
        var anchor = new LayoutRect(45, 830, 100, 30);
        var result = SubtitleLayout.Compute(anchor, Area, 28, Padding, FakeMeasure(80));

        Assert.True(result.Box.X >= Area.X);
        Assert.True(result.Box.Right <= Area.Right);
    }

    [Fact]
    public void AssembleWithBounds_UnionOfKeptRows_IgnoresHud()
    {
        OcrLine[] lines =
        [
            new("Try the whole town.", new PixelRect(700, 10, 400, 30), null),
            new("He's one of four bosses.", new PixelRect(650, 50, 500, 30), null),
            // HUD lệch hẳn sang phải: không tính vào khung.
            new("Sprint Left Shift", new PixelRect(1600, 120, 200, 25), null),
        ];

        var sentence = SentenceAssembler.AssembleWithBounds(lines, 1860);

        Assert.Equal("Try the whole town. He's one of four bosses.", sentence.Text);
        Assert.Equal(new PixelRect(650, 10, 500, 70), sentence.Bounds);
    }

    [Fact]
    public void AssembleWithBounds_NothingKept_NullBounds()
    {
        Assert.Null(SentenceAssembler.AssembleWithBounds([], 1860).Bounds);
    }

    /// <summary>Đo giả: mỗi ký tự rộng nửa cỡ chữ, cao 1.3 cỡ chữ mỗi dòng, xuống dòng khi vượt bề rộng.</summary>
    private static Func<double, double, (double Width, double Height)> FakeMeasure(int characters)
    {
        return (fontSize, maxWidth) =>
        {
            double fullWidth = characters * fontSize * 0.5;
            int lineCount = (int)Math.Ceiling(fullWidth / maxWidth);
            double width = Math.Min(fullWidth, maxWidth);
            return (width, Math.Max(1, lineCount) * fontSize * 1.3);
        };
    }
}
