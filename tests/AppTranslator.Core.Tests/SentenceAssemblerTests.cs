using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Pipeline;

namespace AppTranslator.Core.Tests;

public sealed class SentenceAssemblerTests
{
    // Kích thước giống vùng phụ đề thật (1861x251) và vị trí lấy từ ảnh mẫu 181336 / 181007.
    private const int RegionWidth = 1861;

    private static OcrLine Line(string text, int x, int y, int width, int height = 30)
    {
        return new OcrLine(text, new PixelRect(x, y, width, height), Confidence: null);
    }

    [Fact]
    public void Assemble_TwoCenteredLines_JoinsTopToBottom()
    {
        var lines = new[]
        {
            Line("the King of Beggars.", 806, 905, 230),
            Line("Try the whole town. He's one of four bosses", 120, 868, 1600),
        };

        string sentence = SentenceAssembler.Assemble(lines, RegionWidth);

        Assert.Equal("Try the whole town. He's one of four bosses the King of Beggars.", sentence);
    }

    [Fact]
    public void Assemble_DropsRightAlignedHudLines()
    {
        var lines = new[]
        {
            Line("Ciri: Run into trouble? Should I turn back and help you?!", 640, 20, 515),
            Line("Sprint Left Shift", 1630, 72, 120),
            Line("Jump Space", 1634, 114, 95),
            Line("Witcher Senses [Hold]", 1514, 154, 165),
        };

        string sentence = SentenceAssembler.Assemble(lines, RegionWidth);

        Assert.Equal("Ciri: Run into trouble? Should I turn back and help you?!", sentence);
    }

    [Fact]
    public void Assemble_LineSplitIntoLeftAndRightPieces_IsMergedBeforeCenterCheck()
    {
        // Mỗi mảnh riêng lẻ đều lệch khỏi tâm, nhưng gộp lại thì hàng chữ căn giữa.
        var lines = new[]
        {
            Line("knowledge counts as much as your silver sword.", 1000, 340, 740),
            Line("Young blood craves action, I understand that.", 122, 342, 700),
        };

        string sentence = SentenceAssembler.Assemble(lines, RegionWidth);

        Assert.Equal("Young blood craves action, I understand that. knowledge counts as much as your silver sword.", sentence);
    }

    [Fact]
    public void Assemble_CollapsesRepeatedSpaces()
    {
        var lines = new[] { Line("  Dawn's   some way off.  ", 740, 100, 380) };

        Assert.Equal("Dawn's some way off.", SentenceAssembler.Assemble(lines, RegionWidth));
    }

    [Fact]
    public void Assemble_NoLines_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, SentenceAssembler.Assemble([], RegionWidth));
    }

    [Fact]
    public void Assemble_OnlyHud_ReturnsEmpty()
    {
        var lines = new[] { Line("Sprint Left Shift", 1630, 72, 120) };

        Assert.Equal(string.Empty, SentenceAssembler.Assemble(lines, RegionWidth));
    }
}
