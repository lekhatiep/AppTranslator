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
