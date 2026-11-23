using AppTranslator.Core.Profiles;
using AppTranslator.Core.Settings;
using AppTranslator.Storage;

namespace AppTranslator.Core.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AppTranslatorTests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;

    public StorageTests() => _paths = new AppPaths(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void SeedMissing_LoadsBundledWitcher3Profile()
    {
        var store = new JsonProfileStore(_paths);
        store.SeedMissing(Path.Combine(AppContext.BaseDirectory, "profiles"));

        var profile = store.Get("witcher3");

        Assert.NotNull(profile);
        Assert.Equal("The Witcher 3: Wild Hunt", profile.GameName);
        Assert.Equal(2, profile.Regions.Count);
        Assert.Equal(RegionType.Subtitle, profile.Regions[0].Type);
        Assert.Equal(0.80, profile.Regions[0].Y);
        Assert.Equal("▶", profile.Regions[1].SelectedMarker);
        Assert.Contains("Geralt", profile.KeepNames);
        Assert.Equal(new GlossaryEntry("the Wild Hunt", "Đoàn Săn Hoang Dã"), profile.Glossary[0]);
        Assert.Contains(store.LoadAll(), p => p.Id == "default");
    }

    [Fact]
    public void SeedMissing_DoesNotOverwriteUserEditedProfile()
    {
        var store = new JsonProfileStore(_paths);
        store.Save(new GameProfile { Id = "witcher3", GameName = "Đã sửa" });

        store.SeedMissing(Path.Combine(AppContext.BaseDirectory, "profiles"));

        Assert.Equal("Đã sửa", store.Get("witcher3")!.GameName);
    }

    [Fact]
    public void Profile_SaveThenGet_RoundTrips()
    {
        var store = new JsonProfileStore(_paths);
        var profile = new GameProfile
        {
            Id = "test",
            GameName = "Trò chơi thử",
            Regions = [new Region { Type = RegionType.Choices, X = 0.5, Y = 0.4, W = 0.4, H = 0.3 }],
            Glossary = [new GlossaryEntry("sword", "kiếm")],
            Version = 2,
        };

        store.Save(profile);
        var loaded = store.Get("test")!;

        Assert.Equal(profile.GameName, loaded.GameName);
        Assert.Equal(profile.Regions[0], loaded.Regions[0]);
        Assert.Equal(profile.Glossary[0], loaded.Glossary[0]);
        Assert.Equal(2, loaded.Version);
        Assert.Contains("Trò chơi thử", File.ReadAllText(Path.Combine(_paths.ProfilesDirectory, "test.json")));
    }

    [Fact]
    public void Settings_FirstLoadCreatesDefaults_ThenRoundTrips()
    {
        var store = new JsonSettingsStore(_paths);

        var first = store.Load();
        Assert.True(File.Exists(_paths.SettingsFile));
        Assert.Equal(new AppSettings().Hotkeys, first.Hotkeys);

        store.Save(first with { ActiveProfileId = "witcher3", CaptureEngine = CaptureEngine.DesktopDuplication });
        var reloaded = store.Load();

        Assert.Equal("witcher3", reloaded.ActiveProfileId);
        Assert.Equal(CaptureEngine.DesktopDuplication, reloaded.CaptureEngine);
        Assert.Contains("\"desktopDuplication\"", File.ReadAllText(_paths.SettingsFile));
    }
}
