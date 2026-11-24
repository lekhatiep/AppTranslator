using AppTranslator.Core.Pipeline;

namespace AppTranslator.Core.Tests;

public sealed class OcrVoteTests
{
    [Fact]
    public void SelectMedian_EachCandidateHasDifferentError_PicksTheOneClosestToOthers()
    {
        // Ví dụ thật từ benchmark (ảnh 224327): mỗi cấu hình sai một chỗ khác nhau.
        var candidates = new[]
        {
            "Bring ouryoung damsel to the lower courtyard. She wants to practice? She'll get't0'practice.",
            "Bring our young damsel to the lower courtyard. She wants to practice? Sheill get to practice.",
            "Bring our young damsel to the lower courtyard. She wants to practice? She'll get to practice.",
        };

        Assert.Equal(candidates[2], OcrVote.SelectMedian(candidates));
    }

    [Fact]
    public void SelectMedian_MajorityAgrees_PicksMajority()
    {
        var candidates = new[] { "Dawn's some way off.", "Dawns sorne way of", "Dawn's some way off." };

        Assert.Equal("Dawn's some way off.", OcrVote.SelectMedian(candidates));
    }

    [Fact]
    public void SelectMedian_IgnoresEmptyCandidates()
    {
        var candidates = new[] { "", "Shall we run the walls?", "" };

        Assert.Equal("Shall we run the walls?", OcrVote.SelectMedian(candidates));
    }

    [Fact]
    public void SelectMedian_AllEmptyOrNoCandidates_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, OcrVote.SelectMedian(["", " "]));
        Assert.Equal(string.Empty, OcrVote.SelectMedian([]));
    }

    [Fact]
    public void SelectMedian_SingleCandidate_ReturnsIt()
    {
        Assert.Equal("Strike!", OcrVote.SelectMedian(["Strike!"]));
    }

    [Fact]
    public void SelectMedian_Tie_ReturnsFirst()
    {
        Assert.Equal("a b", OcrVote.SelectMedian(["a b", "a c"]));
    }
}
