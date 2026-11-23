using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Profiles;

namespace AppTranslator.Core.Tests;

public sealed class RegionTests
{
    [Fact]
    public void ToPixelRect_ScalesRatiosToFrameSize()
    {
        var region = new Region { Type = RegionType.Subtitle, X = 0.0, Y = 0.80, W = 1.0, H = 0.12 };

        var rect = region.ToPixelRect(1920, 1080);

        Assert.Equal(new PixelRect(0, 864, 1920, 130), rect);
    }

    [Fact]
    public void ToPixelRect_ClampsRegionThatOverflowsFrame()
    {
        var region = new Region { Type = RegionType.Choices, X = 0.9, Y = -0.1, W = 0.5, H = 0.3 };

        var rect = region.ToPixelRect(1000, 500);

        Assert.Equal(new PixelRect(900, 0, 100, 100), rect);
    }

    [Fact]
    public void ToPixelRect_RegionFullyOutsideFrame_ReturnsEmptySize()
    {
        var region = new Region { Type = RegionType.Subtitle, X = 1.2, Y = 0.5, W = 0.1, H = 0.1 };

        var rect = region.ToPixelRect(1000, 500);

        Assert.Equal(0, rect.Width);
    }

    [Fact]
    public void WithPixelRect_KeepsOtherPropertiesAndRoundTrips()
    {
        var original = new Region { Type = RegionType.Choices, TextColor = "yellow", SelectedMarker = "▶" };
        var selected = new PixelRect(1056, 432, 768, 324);

        var updated = original.WithPixelRect(selected, 1920, 1080);

        Assert.Equal(0.55, updated.X);
        Assert.Equal(0.4, updated.Y);
        Assert.Equal(0.4, updated.W);
        Assert.Equal(0.3, updated.H);
        Assert.Equal("yellow", updated.TextColor);
        Assert.Equal("▶", updated.SelectedMarker);
        Assert.Equal(selected, updated.ToPixelRect(1920, 1080));
    }

    [Fact]
    public void WithPixelRect_RoundsToFourDecimals()
    {
        var region = new Region { Type = RegionType.Subtitle };

        var updated = region.WithPixelRect(new PixelRect(1, 1, 1, 1), 3, 3);

        Assert.Equal(0.3333, updated.X);
    }

    [Fact]
    public void WithPixelRect_ThenToPixelRect_StaysWithinOnePixelOn4K()
    {
        var selected = new PixelRect(1237, 1781, 1111, 207);

        var rect = new Region { Type = RegionType.Subtitle }.WithPixelRect(selected, 3840, 2160).ToPixelRect(3840, 2160);

        Assert.InRange(rect.X, selected.X - 1, selected.X + 1);
        Assert.InRange(rect.Y, selected.Y - 1, selected.Y + 1);
        Assert.InRange(rect.Width, selected.Width - 1, selected.Width + 1);
        Assert.InRange(rect.Height, selected.Height - 1, selected.Height + 1);
    }
}
