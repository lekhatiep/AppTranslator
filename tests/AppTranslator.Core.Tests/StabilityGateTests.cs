using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Pipeline;

namespace AppTranslator.Core.Tests;

public class StabilityGateTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly TextMask Empty = TextMask.FromFrame(TestFrames.Dark(400, 80));
    private static readonly TextMask SentenceA = TextMask.FromFrame(TestFrames.WithBlocks(400, 80, new PixelRect(40, 24, 160, 32)));
    private static readonly TextMask SentenceB = TextMask.FromFrame(TestFrames.WithBlocks(400, 80, new PixelRect(200, 24, 160, 32)));

    [Fact]
    public void StillText_StableOnceAfterDuration()
    {
        var gate = new StabilityGate(TimeSpan.FromMilliseconds(300));

        Assert.Equal(SubtitleChange.Changed, gate.Update(SentenceA, At(0)));
        Assert.Equal(SubtitleChange.None, gate.Update(SentenceA, At(100)));
        Assert.Equal(SubtitleChange.None, gate.Update(SentenceA, At(200)));
        Assert.Equal(SubtitleChange.Stable, gate.Update(SentenceA, At(300)));
        Assert.Equal(SubtitleChange.None, gate.Update(SentenceA, At(400)));
        Assert.Equal(At(0), gate.StillSince);
    }

    [Fact]
    public void TextChangesBeforeDuration_WaitsAgain()
    {
        var gate = new StabilityGate(TimeSpan.FromMilliseconds(300));

        gate.Update(SentenceA, At(0));
        Assert.Equal(SubtitleChange.Changed, gate.Update(SentenceB, At(200)));
        Assert.Equal(SubtitleChange.None, gate.Update(SentenceB, At(400)));
        Assert.Equal(SubtitleChange.Stable, gate.Update(SentenceB, At(500)));
        Assert.Equal(At(200), gate.StillSince);
    }

    [Fact]
    public void TextDisappears_ClearedAfterDuration()
    {
        var gate = new StabilityGate(TimeSpan.FromMilliseconds(300));
        gate.Update(SentenceA, At(0));
        gate.Update(SentenceA, At(300));

        Assert.Equal(SubtitleChange.Changed, gate.Update(Empty, At(1000)));
        Assert.Equal(SubtitleChange.Cleared, gate.Update(Empty, At(1300)));
        Assert.Equal(SubtitleChange.None, gate.Update(Empty, At(1600)));
    }

    [Fact]
    public void EmptyFromStart_NeverReports()
    {
        var gate = new StabilityGate(TimeSpan.FromMilliseconds(300));

        Assert.Equal(SubtitleChange.Changed, gate.Update(Empty, At(0)));
        Assert.Equal(SubtitleChange.None, gate.Update(Empty, At(300)));
        Assert.Equal(SubtitleChange.None, gate.Update(Empty, At(600)));
    }

    [Fact]
    public void NewSentenceAfterStable_StableAgain()
    {
        var gate = new StabilityGate(TimeSpan.FromMilliseconds(300));
        gate.Update(SentenceA, At(0));
        gate.Update(SentenceA, At(300));

        Assert.Equal(SubtitleChange.Changed, gate.Update(SentenceB, At(2000)));
        Assert.Equal(SubtitleChange.Stable, gate.Update(SentenceB, At(2300)));
    }

    private static DateTimeOffset At(int milliseconds)
    {
        return Start.AddMilliseconds(milliseconds);
    }
}
