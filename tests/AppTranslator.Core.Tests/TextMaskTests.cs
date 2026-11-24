using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Pipeline;

namespace AppTranslator.Core.Tests;

public class TextMaskTests
{
    [Fact]
    public void DarkImage_HasNoText()
    {
        var mask = TextMask.FromFrame(TestFrames.Dark(400, 80));

        Assert.False(mask.HasText);
        Assert.Equal(0, mask.TextCellCount);
    }

    [Fact]
    public void WhiteBlock_IsText()
    {
        var mask = TextMask.FromFrame(TestFrames.WithBlocks(400, 80, new PixelRect(40, 24, 160, 32)));

        Assert.True(mask.HasText);
        Assert.Equal(20 * 4, mask.TextCellCount);
    }

    [Fact]
    public void SameImage_NoChange()
    {
        var first = TextMask.FromFrame(TestFrames.WithBlocks(400, 80, new PixelRect(40, 24, 160, 32)));
        var second = TextMask.FromFrame(TestFrames.WithBlocks(400, 80, new PixelRect(40, 24, 160, 32)));

        Assert.Equal(0.0, first.ChangeRatio(second));
    }

    [Fact]
    public void DifferentText_LargeChange()
    {
        var first = TextMask.FromFrame(TestFrames.WithBlocks(400, 80, new PixelRect(40, 24, 160, 32)));
        var second = TextMask.FromFrame(TestFrames.WithBlocks(400, 80, new PixelRect(200, 24, 160, 32)));

        Assert.Equal(1.0, first.ChangeRatio(second));
    }

    [Fact]
    public void OneBrightPixelPerCell_NotText()
    {
        // Nhiễu nén video lẻ tẻ không được tính là chữ.
        var frame = TestFrames.Dark(400, 80);
        for (int y = 0; y < 80; y += 8)
        {
            for (int x = 0; x < 400; x += 8)
                TestFrames.SetWhite(frame, x, y);
        }

        Assert.Equal(0, TextMask.FromFrame(frame).TextCellCount);
    }

    [Fact]
    public void LowerThreshold_CountsDimYellowText()
    {
        // Chữ lựa chọn vàng sẫm (luma ~165): không tính ở ngưỡng phụ đề 200, có tính ở ngưỡng 150.
        var frame = TestFrames.Dark(400, 80);
        for (int y = 24; y < 56; y++)
        {
            for (int x = 40; x < 200; x++)
            {
                int offset = y * frame.Stride + x * 4;
                frame.Pixels[offset] = 40;
                frame.Pixels[offset + 1] = 175;
                frame.Pixels[offset + 2] = 220;
            }
        }

        Assert.Equal(0, TextMask.FromFrame(frame).TextCellCount);
        Assert.Equal(20 * 4, TextMask.FromFrame(frame, minTextLuma: 150).TextCellCount);
    }

    [Fact]
    public void DifferentSize_FullChange()
    {
        var first = TextMask.FromFrame(TestFrames.Dark(400, 80));
        var second = TextMask.FromFrame(TestFrames.Dark(320, 80));

        Assert.Equal(1.0, first.ChangeRatio(second));
    }
}

internal static class TestFrames
{
    public static Frame Dark(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (int i = 3; i < pixels.Length; i += 4)
            pixels[i] = 255;

        return new Frame(pixels, width, height, width * 4, DateTimeOffset.Now);
    }

    public static Frame WithBlocks(int width, int height, params PixelRect[] blocks)
    {
        var frame = Dark(width, height);
        foreach (var block in blocks)
        {
            for (int y = block.Y; y < block.Y + block.Height; y++)
            {
                for (int x = block.X; x < block.X + block.Width; x++)
                    SetWhite(frame, x, y);
            }
        }

        return frame;
    }

    public static void SetWhite(Frame frame, int x, int y)
    {
        int offset = y * frame.Stride + x * 4;
        frame.Pixels[offset] = 255;
        frame.Pixels[offset + 1] = 255;
        frame.Pixels[offset + 2] = 255;
    }
}
