using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Pipeline;

namespace AppTranslator.Core.Tests;

public class ChoiceParserTests
{
    [Fact]
    public void NumberedLines_SplitIntoChoices()
    {
        // Kết quả PaddleOCR thật trên ảnh 180920 (vùng lựa chọn).
        OcrLine[] lines =
        [
            new("1. Take it the heist was a bust?", new PixelRect(43, 101, 332, 38), null),
            new("2. Reuven? Who's that?", new PixelRect(48, 152, 249, 34), null),
            new("3. Breaking into a vault.  what was Dandelion thinking?", new PixelRect(48, 200, 578, 28), null),
        ];

        var choices = ChoiceParser.Parse(lines);

        Assert.Equal(3, choices.Count);
        Assert.Equal(new DialogueChoice(1, "Take it the heist was a bust?", new PixelRect(43, 101, 332, 38)), choices[0]);
        Assert.Equal(2, choices[1].Number);
        Assert.Equal("Reuven? Who's that?", choices[1].Text);
        Assert.Equal("Breaking into a vault. what was Dandelion thinking?", choices[2].Text);
    }

    [Theory]
    [InlineData("▶ 1. I dreamt of Kaer Morhen...", 1, "I dreamt of Kaer Morhen...")]
    [InlineData("» 2) Really rather not talk about it.", 2, "Really rather not talk about it.")]
    [InlineData("3,Of course. [BEGIN TUTORIAL]", 3, "Of course. [BEGIN TUTORIAL]")]
    [InlineData("12. Leave.", 12, "Leave.")]
    public void MarkerAndNumberVariants_Stripped(string ocrText, int number, string text)
    {
        var choices = ChoiceParser.Parse([new OcrLine(ocrText, new PixelRect(40, 100, 300, 36), null)]);

        Assert.Equal(number, Assert.Single(choices).Number);
        Assert.Equal(text, choices[0].Text);
    }

    [Fact]
    public void LineWithoutNumber_AppendedToPreviousChoice()
    {
        OcrLine[] lines =
        [
            new("1. That alliance still alive? What happened", new PixelRect(50, 100, 500, 36), null),
            new("to Letho?", new PixelRect(70, 140, 120, 36), null),
            new("2. Leave.", new PixelRect(50, 190, 120, 36), null),
        ];

        var choices = ChoiceParser.Parse(lines);

        Assert.Equal(2, choices.Count);
        Assert.Equal("That alliance still alive? What happened to Letho?", choices[0].Text);
        Assert.Equal(new PixelRect(50, 100, 500, 76), choices[0].Bounds);
    }

    [Fact]
    public void FragmentsOnSameRow_Merged()
    {
        OcrLine[] lines =
        [
            new("Take it the heist", new PixelRect(80, 100, 200, 36), null),
            new("1.", new PixelRect(43, 101, 25, 36), null),
            new("was a bust?", new PixelRect(290, 100, 120, 36), null),
        ];

        var choice = Assert.Single(ChoiceParser.Parse(lines));

        Assert.Equal(1, choice.Number);
        Assert.Equal("Take it the heist was a bust?", choice.Text);
    }

    [Fact]
    public void NoNumberAtAll_KeptAsUnnumberedChoice()
    {
        var choice = Assert.Single(ChoiceParser.Parse([new OcrLine("Leave.", new PixelRect(40, 100, 100, 36), null)]));

        Assert.Null(choice.Number);
        Assert.Equal("Leave.", choice.Text);
    }

    [Fact]
    public void Empty_NoChoices()
    {
        Assert.Empty(ChoiceParser.Parse([]));
    }
}

public class SelectionMarkerTests
{
    private static readonly PixelRect Row1 = new(43, 100, 300, 36);
    private static readonly PixelRect Row2 = new(48, 150, 300, 36);

    [Fact]
    public void YellowMarkerLeftOfRow_Selected()
    {
        var frame = TestFrames.Dark(686, 230);
        FillYellow(frame, new PixelRect(25, 160, 9, 12));

        Assert.Equal(1, SelectionMarker.FindSelected(frame, [Row1, Row2]));
    }

    [Fact]
    public void NoMarker_Null()
    {
        Assert.Null(SelectionMarker.FindSelected(TestFrames.Dark(686, 230), [Row1, Row2]));
    }

    [Fact]
    public void YellowTextItself_NotCounted()
    {
        // Lựa chọn chữ vàng nhưng không có ▶: chỉ xét dải bên trái dòng nên chữ không bị tính.
        var frame = TestFrames.Dark(686, 230);
        FillYellow(frame, new PixelRect(Row1.X, Row1.Y, 300, 36));

        Assert.Null(SelectionMarker.FindSelected(frame, [Row1, Row2]));
    }

    [Fact]
    public void YellowBackgroundOnBothRows_Ambiguous_Null()
    {
        var frame = TestFrames.Dark(686, 230);
        FillYellow(frame, new PixelRect(0, 100, 40, 90));

        Assert.Null(SelectionMarker.FindSelected(frame, [Row1, Row2]));
    }

    private static void FillYellow(Frame frame, PixelRect area)
    {
        for (int y = area.Y; y < area.Y + area.Height; y++)
        {
            for (int x = area.X; x < area.X + area.Width; x++)
            {
                int offset = y * frame.Stride + x * 4;
                frame.Pixels[offset] = 30;
                frame.Pixels[offset + 1] = 170;
                frame.Pixels[offset + 2] = 230;
            }
        }
    }
}
