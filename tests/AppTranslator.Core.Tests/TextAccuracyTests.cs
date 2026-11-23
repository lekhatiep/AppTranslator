using AppTranslator.Core.Evaluation;

namespace AppTranslator.Core.Tests;

public sealed class TextAccuracyTests
{
    [Fact]
    public void WordAccuracy_IdenticalText_IsOne()
    {
        Assert.Equal(1.0, TextAccuracy.WordAccuracy("Dawn's some way off.", "Dawn's some way off.", ignoreCaseAndPunctuation: false));
    }

    [Fact]
    public void WordAccuracy_OneWrongWordOutOfFour_IsThreeQuarters()
    {
        double accuracy = TextAccuracy.WordAccuracy("Shall we run walls", "Shall we ran walls", ignoreCaseAndPunctuation: false);

        Assert.Equal(0.75, accuracy, precision: 10);
    }

    [Fact]
    public void WordAccuracy_MissingAndExtraWords_CountAsErrors()
    {
        // thiếu "some", thừa "now": 2 lỗi trên 4 từ chuẩn
        double accuracy = TextAccuracy.WordAccuracy("Dawn's some way off", "Dawn's way off now", ignoreCaseAndPunctuation: false);

        Assert.Equal(0.5, accuracy, precision: 10);
    }

    [Fact]
    public void WordAccuracy_StrictMode_PunctuationDifferenceIsAnError()
    {
        double strict = TextAccuracy.WordAccuracy("We've got time.", "We've got time", ignoreCaseAndPunctuation: false);
        double loose = TextAccuracy.WordAccuracy("We've got time.", "we've GOT time", ignoreCaseAndPunctuation: true);

        Assert.Equal(2.0 / 3.0, strict, precision: 10);
        Assert.Equal(1.0, loose);
    }

    [Fact]
    public void WordAccuracy_ChoiceSeparatorTreatedAsSpace()
    {
        double accuracy = TextAccuracy.WordAccuracy("1. Don't get mad at her.|2. We've spoiled her.", "1. Don't get mad at her. 2. We've spoiled her.", ignoreCaseAndPunctuation: false);

        Assert.Equal(1.0, accuracy);
    }

    [Fact]
    public void WordAccuracy_LooseMode_DropsPunctuationOnlyTokens()
    {
        // "-" và "▶" đọc thừa không tính lỗi ở chế độ bỏ dấu câu
        double accuracy = TextAccuracy.WordAccuracy("underworld - the others", "▶ underworld the others", ignoreCaseAndPunctuation: true);

        Assert.Equal(1.0, accuracy);
    }

    [Fact]
    public void WordAccuracy_CompletelyWrongOrLonger_NeverBelowZero()
    {
        double accuracy = TextAccuracy.WordAccuracy("yes", "no no no no", ignoreCaseAndPunctuation: false);

        Assert.Equal(0.0, accuracy);
    }

    [Theory]
    [InlineData("", "", 1.0)]
    [InlineData("", "noise", 0.0)]
    [InlineData("text", "", 0.0)]
    public void WordAccuracy_EmptyInputs(string expected, string actual, double accuracyExpected)
    {
        Assert.Equal(accuracyExpected, TextAccuracy.WordAccuracy(expected, actual, ignoreCaseAndPunctuation: false));
    }
}
