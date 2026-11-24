using AppTranslator.Core.Pipeline;

namespace AppTranslator.Core.Tests;

public class SentenceComparerTests
{
    [Theory]
    // Các biến thể OCR thật của cùng một câu (video 24/11, nền chuyển động).
    [InlineData("But you were asleep, Uncle Vesemir.", "Butyou were asleep, Uncle Vesemir.")]
    [InlineData("But you were asleep, Uncle Vesemir.", "But you were asleep, Uncle Nesemir.")]
    [InlineData("But you were asleep, Uncle Vesemir.", "But-you were asleep, UnCle Vesemir.")]
    [InlineData("Bring our young damsel to the lower courtyard.", "Bring ouryoung dåmsel to the lower courtyard.")]
    [InlineData("", "")]
    public void SmallOcrDifferences_SameSentence(string first, string second)
    {
        Assert.True(SentenceComparer.AreSameSentence(first, second));
    }

    [Theory]
    [InlineData("Dawn's some way off. We've got time.", "Shall we run the walls?")]
    [InlineData("Yes.", "No.")]
    [InlineData("But you were asleep, Uncle Vesemir.", "")]
    [InlineData("Bring our young damsel to the lower courtyard.", "Bring our young damsel to the upper courtyard. She wants to practice?")]
    public void DifferentSentences_NotSame(string first, string second)
    {
        Assert.False(SentenceComparer.AreSameSentence(first, second));
    }
}
