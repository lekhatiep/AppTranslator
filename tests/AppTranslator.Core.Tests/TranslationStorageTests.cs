using System.Runtime.Versioning;
using System.Text;
using AppTranslator.Core.Abstractions;
using AppTranslator.Storage;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AppTranslator.Core.Tests;

public sealed class SqliteTranslationCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AppTranslatorTests", Guid.NewGuid().ToString("N"));
    private readonly SqliteTranslationCache _cache;

    public SqliteTranslationCacheTests()
    {
        _cache = new SqliteTranslationCache(Path.Combine(_root, "data", "translator.db"));
    }

    public void Dispose()
    {
        // Pool kết nối của SQLite giữ file, phải xả trước khi xóa thư mục tạm.
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task SetThenGet_MatchesNormalizedSource_AndCountsHits()
    {
        var profile = TestProfiles.Witcher3();
        await _cache.SetAsync(profile, "But you were asleep, Uncle Vesemir.", new TranslationResult("Nhưng chú đang ngủ mà.", "Gemini", "m1"));

        var hit = await _cache.GetAsync(profile, "Butyou were asleep, Uncle Vesemir");
        await _cache.GetAsync(profile, "But you were asleep, Uncle Vesemir.");

        Assert.Equal(new TranslationResult("Nhưng chú đang ngủ mà.", "Gemini", "m1"), hit);
        Assert.Equal(2, await ReadHitCountAsync());
    }

    [Fact]
    public async Task OtherProfileOrVersion_Miss()
    {
        var profile = TestProfiles.Witcher3();
        await _cache.SetAsync(profile, "Hm? What?", new TranslationResult("Hả? Gì?", "Gemini", "m1"));

        Assert.Null(await _cache.GetAsync(profile with { Version = 2 }, "Hm? What?"));
        Assert.Null(await _cache.GetAsync(profile with { Id = "other" }, "Hm? What?"));
    }

    [Fact]
    public async Task SetTwice_Overwrites()
    {
        var profile = TestProfiles.Witcher3();
        await _cache.SetAsync(profile, "Hm? What?", new TranslationResult("Hả?", "Gemini", "m1"));
        await _cache.SetAsync(profile, "Hm? What?", new TranslationResult("Hả? Gì thế?", "Gemini", "m2"));

        Assert.Equal("Hả? Gì thế?", (await _cache.GetAsync(profile, "Hm? What?"))!.TranslatedText);
    }

    [Fact]
    public async Task Clear_RemovesOnlyThatProfile()
    {
        var witcher = TestProfiles.Witcher3();
        var other = witcher with { Id = "other" };
        await _cache.SetAsync(witcher, "Hm? What?", new TranslationResult("Hả?", "Gemini", "m"));
        await _cache.SetAsync(other, "Hm? What?", new TranslationResult("Hả?", "Gemini", "m"));

        await _cache.ClearAsync("witcher3");

        Assert.Null(await _cache.GetAsync(witcher, "Hm? What?"));
        Assert.NotNull(await _cache.GetAsync(other, "Hm? What?"));
    }

    private async Task<int> ReadHitCountAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={Path.Combine(_root, "data", "translator.db")}");
        return await connection.ExecuteScalarAsync<int>("SELECT HitCount FROM TranslationCache");
    }
}

public sealed class SqliteHistoryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AppTranslatorTests", Guid.NewGuid().ToString("N"));
    private readonly SqliteHistoryStore _store;

    public SqliteHistoryStoreTests()
    {
        _store = new SqliteHistoryStore(Path.Combine(_root, "data", "translator.db"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Add_ThenGetRecent_NewestFirst_WithTiming()
    {
        var now = DateTimeOffset.Now;
        var timing = new PipelineTiming(320, 50, 800, 5, 1175, "WindowsOcr");
        await _store.AddAsync(new HistoryEntry(now.AddSeconds(-5), "witcher3", "Subtitle", "Hm? What?", "Hả? Gì?", false, timing));
        await _store.AddAsync(new HistoryEntry(now, "witcher3", "Subtitle", "End of discussion.", "Hết chuyện.", true, null));

        var recent = await _store.GetRecentAsync(10);

        Assert.Equal(2, recent.Count);
        Assert.Equal("End of discussion.", recent[0].SourceText);
        Assert.True(recent[0].FromCache);
        Assert.Null(recent[0].Timing);
        Assert.Equal(timing, recent[1].Timing);
        Assert.Equal(now.AddSeconds(-5).ToUnixTimeMilliseconds(), recent[1].CreatedAt.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task GetRecent_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
            await _store.AddAsync(new HistoryEntry(DateTimeOffset.Now, "witcher3", "Subtitle", $"Line {i}", $"Câu {i}", false, null));

        var recent = await _store.GetRecentAsync(3);

        Assert.Equal(["Line 4", "Line 3", "Line 2"], recent.Select(entry => entry.SourceText));
    }

    [Fact]
    public async Task DeleteOlderThan_RemovesOldEntriesAndTheirMetrics()
    {
        var now = DateTimeOffset.Now;
        var timing = new PipelineTiming(1, 1, 1, 1, 4, "WindowsOcr");
        await _store.AddAsync(new HistoryEntry(now.AddDays(-10), "witcher3", "Subtitle", "Old.", "Cũ.", false, timing));
        await _store.AddAsync(new HistoryEntry(now, "witcher3", "Subtitle", "New.", "Mới.", false, timing));

        await _store.DeleteOlderThanAsync(now.AddDays(-7));

        var recent = await _store.GetRecentAsync(10);
        Assert.Equal(["New."], recent.Select(entry => entry.SourceText));
        await using var connection = new SqliteConnection($"Data Source={Path.Combine(_root, "data", "translator.db")}");
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM PipelineMetric"));
    }
}

[SupportedOSPlatform("windows")]
public sealed class SecretStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AppTranslatorTests", Guid.NewGuid().ToString("N"));
    private readonly AppPaths _paths;

    public SecretStoreTests()
    {
        _paths = new AppPaths(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void SetThenGet_RoundTrips_AndFileIsNotPlainText()
    {
        new SecretStore(_paths).Set(SecretStore.GeminiApiKey, "  AIza-test-key  ");

        Assert.Equal("AIza-test-key", new SecretStore(_paths).Get(SecretStore.GeminiApiKey));
        string fileContent = Encoding.UTF8.GetString(File.ReadAllBytes(_paths.SecretsFile));
        Assert.DoesNotContain("AIza-test-key", fileContent);
    }

    [Fact]
    public void Missing_ReturnsNull_AndEmptyValueRemoves()
    {
        var store = new SecretStore(_paths);
        Assert.Null(store.Get(SecretStore.GeminiApiKey));

        store.Set(SecretStore.GeminiApiKey, "key");
        store.Set(SecretStore.GeminiApiKey, "");

        Assert.Null(store.Get(SecretStore.GeminiApiKey));
    }
}
