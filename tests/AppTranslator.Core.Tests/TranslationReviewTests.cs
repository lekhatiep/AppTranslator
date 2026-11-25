using AppTranslator.Core.Evaluation;

namespace AppTranslator.Core.Tests;

public class TranslationReviewTests
{
    [Theory]
    [InlineData("Đạt", true)]
    [InlineData(" dat ", true)]
    [InlineData("ĐẠT", true)]
    [InlineData("Sai", false)]
    [InlineData("sai", false)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("?", null)]
    public void ParseVerdict(string? verdict, bool? expected)
    {
        Assert.Equal(expected, TranslationReview.ParseVerdict(verdict));
    }

    [Fact]
    public void Score_PerRegionType_IgnoresUngraded()
    {
        var scores = TranslationReview.Score(
        [
            ("Subtitle", "Đạt"),
            ("Subtitle", "Đạt"),
            ("Subtitle", "Sai"),
            ("Subtitle", ""),
            ("Choices", "Đạt"),
        ]);

        Assert.Equal(new ReviewScore(2, 3, 4), scores["Subtitle"]);
        Assert.Equal(2.0 / 3, scores["Subtitle"].PassRate!.Value, precision: 6);
        Assert.Equal(new ReviewScore(1, 1, 1), scores["Choices"]);
    }

    [Fact]
    public void Score_NothingGraded_NullRate()
    {
        var scores = TranslationReview.Score([("Subtitle", null)]);

        Assert.Null(scores["Subtitle"].PassRate);
    }
}
