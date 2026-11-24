using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Profiles;
using AppTranslator.Core.Text;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AppTranslator.Storage;

/// <summary>
/// Cache bản dịch trong data\translator.db (FR-10, mục 7 tài liệu). Khóa = (ProfileId, SourceHash, ProfileVersion):
/// SourceHash là SHA-256 của câu đã chuẩn hóa, nên "Butyou were asleep" và "But you were asleep" dùng chung bản dịch;
/// sửa hồ sơ (tăng Version) thì cache cũ không bị dùng lại.
/// </summary>
public sealed class SqliteTranslationCache : ITranslationCache
{
    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS TranslationCache (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProfileId TEXT NOT NULL,
            SourceHash TEXT NOT NULL,
            SourceText TEXT NOT NULL,
            TranslatedText TEXT NOT NULL,
            Provider TEXT NOT NULL,
            Model TEXT NOT NULL,
            ProfileVersion INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            HitCount INTEGER NOT NULL DEFAULT 0,
            UNIQUE (ProfileId, SourceHash, ProfileVersion)
        );
        """;

    private readonly string _connectionString;
    private bool _created;

    public SqliteTranslationCache(string databaseFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databaseFile))!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databaseFile }.ToString();
    }

    public async Task<TranslationResult?> GetAsync(GameProfile profile, string sourceText, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var key = new { ProfileId = profile.Id, SourceHash = Hash(sourceText), ProfileVersion = profile.Version };
        var row = await connection.QuerySingleOrDefaultAsync<CacheRow>(new CommandDefinition(
            "SELECT TranslatedText, Provider, Model FROM TranslationCache " +
            "WHERE ProfileId = @ProfileId AND SourceHash = @SourceHash AND ProfileVersion = @ProfileVersion",
            key, cancellationToken: cancellationToken));
        if (row is null)
            return null;

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE TranslationCache SET HitCount = HitCount + 1 " +
            "WHERE ProfileId = @ProfileId AND SourceHash = @SourceHash AND ProfileVersion = @ProfileVersion",
            key, cancellationToken: cancellationToken));
        return new TranslationResult(row.TranslatedText, row.Provider, row.Model);
    }

    public async Task SetAsync(GameProfile profile, string sourceText, TranslationResult result, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        // Cùng khóa thì ghi đè (vd dịch lại bằng model khác).
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO TranslationCache (ProfileId, SourceHash, SourceText, TranslatedText, Provider, Model, ProfileVersion, CreatedAt) " +
            "VALUES (@ProfileId, @SourceHash, @SourceText, @TranslatedText, @Provider, @Model, @ProfileVersion, @CreatedAt) " +
            "ON CONFLICT (ProfileId, SourceHash, ProfileVersion) DO UPDATE SET " +
            "SourceText = excluded.SourceText, TranslatedText = excluded.TranslatedText, Provider = excluded.Provider, " +
            "Model = excluded.Model, CreatedAt = excluded.CreatedAt",
            new
            {
                ProfileId = profile.Id,
                SourceHash = Hash(sourceText),
                SourceText = sourceText,
                result.TranslatedText,
                result.Provider,
                result.Model,
                ProfileVersion = profile.Version,
                CreatedAt = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
            },
            cancellationToken: cancellationToken));
    }

    public async Task ClearAsync(string profileId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM TranslationCache WHERE ProfileId = @ProfileId", new { ProfileId = profileId }, cancellationToken: cancellationToken));
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        if (!_created)
        {
            await connection.ExecuteAsync(new CommandDefinition(CreateTableSql, cancellationToken: cancellationToken));
            _created = true;
        }

        return connection;
    }

    private static string Hash(string sourceText)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(TextNormalizer.ForComparison(sourceText)));
        return Convert.ToHexString(bytes);
    }

    private sealed record CacheRow(string TranslatedText, string Provider, string Model);
}
