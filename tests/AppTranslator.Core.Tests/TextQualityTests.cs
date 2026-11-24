using AppTranslator.Core.Text;

namespace AppTranslator.Core.Tests;

public class TextQualityTests
{
    [Theory]
    [InlineData("But you were asleep, Uncle Vesemir.", "butyouwereasleepunclevesemir")]
    [InlineData("Butyou were asleep, UnCle Vesemir", "butyouwereasleepunclevesemir")]
    [InlineData("  ...  ", "")]
    public void ForComparison_IgnoresSpacesPunctuationAndCase(string text, string expected)
    {
        Assert.Equal(expected, TextNormalizer.ForComparison(text));
    }

    [Theory]
    // Lỗi nhẹ thật trong log 24/11: AI tự hiểu được, vẫn gửi dịch.
    [InlineData("Butyou were asleep, Uncle Nesemir.")]
    [InlineData("ÄJgh. I'm sorry. It won'fhappen again.")]
    [InlineData("John df8rugge lacks flair, true, blithe's reliable. Not like the hogwash they print nowadays.")]
    [InlineData("\"We've\" spoiled her? I've been saying it from the start - Ciri needs a firm hand.")]
    [InlineData("Dammit... Had her taking notes on \"Ghouls and Alghouls\"... Wanted to rest my eyes a bit.")]
    [InlineData("Hm?What?")]
    [InlineData("Bring our young damsel to the lower courtyard.She wants to practice?")]
    public void LooksLikeGarbage_SmallOcrErrors_False(string text)
    {
        Assert.False(OcrQuality.LooksLikeGarbage(text));
    }

    [Theory]
    // Chữ rác thật trong log/benchmark (nền sáng sau chữ).
    [InlineData("'Viljåble. Sh&uldjl turn backan 11")]
    [InlineData("")]
    public void LooksLikeGarbage_Garbage_True(string text)
    {
        Assert.True(OcrQuality.LooksLikeGarbage(text));
    }
}
