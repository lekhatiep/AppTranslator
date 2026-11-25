using AppTranslator.Core.Profiles;

namespace AppTranslator.Core.Tests;

public class ProfileTextTests
{
    [Fact]
    public void ParseNames_TrimsSkipsEmptyAndDuplicates()
    {
        var names = ProfileText.ParseNames("Geralt\r\n  Yennefer \n\ngeralt\nKaer Morhen");

        Assert.Equal(["Geralt", "Yennefer", "Kaer Morhen"], names);
    }

    [Fact]
    public void Names_RoundTrip()
    {
        string[] names = ["Geralt", "Ciri"];

        Assert.Equal(names, ProfileText.ParseNames(ProfileText.FormatNames(names)));
    }

    [Fact]
    public void ParseGlossary_ValidLines()
    {
        var errors = new List<string>();

        var glossary = ProfileText.ParseGlossary("the Wild Hunt = Đoàn Săn Hoang Dã\n\nGhouls and Alghouls=Ghoul và Alghoul", errors);

        Assert.Empty(errors);
        Assert.Equal([new GlossaryEntry("the Wild Hunt", "Đoàn Săn Hoang Dã"), new GlossaryEntry("Ghouls and Alghouls", "Ghoul và Alghoul")], glossary);
    }

    [Fact]
    public void ParseGlossary_BadLines_ReportedWithLineNumber()
    {
        var errors = new List<string>();

        var glossary = ProfileText.ParseGlossary("witcher = thợ săn quái vật\nstriga\n= không có tiếng Anh", errors);

        Assert.Single(glossary);
        Assert.Equal(2, errors.Count);
        Assert.Contains("dòng 2", errors[0]);
        Assert.Contains("dòng 3", errors[1]);
    }

    [Fact]
    public void Glossary_RoundTrip()
    {
        GlossaryEntry[] glossary = [new("the Wild Hunt", "Đoàn Săn Hoang Dã")];

        Assert.Equal(glossary, ProfileText.ParseGlossary(ProfileText.FormatGlossary(glossary), []));
    }

    [Theory]
    [InlineData("The Witcher 3: Wild Hunt", "the-witcher-3-wild-hunt")]
    [InlineData("  Elden Ring  ", "elden-ring")]
    [InlineData("ペルソナ", "game")]
    public void CreateId_Slug(string gameName, string expected)
    {
        Assert.Equal(expected, ProfileText.CreateId(gameName, []));
    }

    [Fact]
    public void CreateId_AvoidsExisting()
    {
        Assert.Equal("elden-ring-3", ProfileText.CreateId("Elden Ring", ["elden-ring", "ELDEN-RING-2"]));
    }
}
