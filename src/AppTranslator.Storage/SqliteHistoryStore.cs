using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace AppTranslator.Storage;

/// <summary>Thời gian từng bước của một câu (ms), để đo AC-05 và tinh chỉnh.</summary>
/// <param name="CaptureMs">Từ khung đầu tiên thấy câu tới lúc bắt đầu OCR (chụp + chờ chữ đứng yên).</param>
/// <param name="TotalMs">Từ khung đầu tiên thấy câu tới lúc bản dịch hiện lên.</param>
public sealed record PipelineTiming(double CaptureMs, double OcrMs, double TranslateMs, double RenderMs, double TotalMs, string OcrEngine);

public sealed record HistoryEntry(
    DateTimeOffset CreatedAt,
    string ProfileId,
    string RegionType,
    string SourceText,
    string TranslatedText,
    bool FromCache,
    PipelineTiming? Timing);

/// <summary>Lịch sử dịch và số đo độ trễ trong data\translator.db (FR-12, mục 7 tài liệu).</summary>
public sealed class SqliteHistoryStore
{
    private const string CreateTablesSql = """
        CREATE TABLE IF NOT EXISTS TranslationHistory (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProfileId TEXT NOT NULL,
            RegionType TEXT NOT NULL,
            SourceText TEXT NOT NULL,
            TranslatedText TEXT NOT NULL,
            FromCache INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS PipelineMetric (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            HistoryId INTEGER NOT NULL REFERENCES TranslationHistory(Id) ON DELETE CASCADE,
            CaptureMs REAL NOT NULL,
            OcrMs REAL NOT NULL,
            TranslateMs REAL NOT NULL,
            RenderMs REAL NOT NULL,
            TotalMs REAL NOT NULL,
            OcrEngine TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_TranslationHistory_CreatedAt ON TranslationHistory(CreatedAt);
        """;

    private readonly string _connectionString;
    private bool _created;

    public SqliteHistoryStore(string databaseFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databaseFile))!);
        // Foreign Keys = true để xóa lịch sử cũ thì số đo đi kèm cũng bị xóa.
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databaseFile, ForeignKeys = true }.ToString();
    }

    public async Task AddAsync(HistoryEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        string createdAt = Format(entry.CreatedAt);
        long historyId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "INSERT INTO TranslationHistory (ProfileId, RegionType, SourceText, TranslatedText, FromCache, CreatedAt) " +
            "VALUES (@ProfileId, @RegionType, @SourceText, @TranslatedText, @FromCache, @CreatedAt); SELECT last_insert_rowid();",
            new { entry.ProfileId, entry.RegionType, entry.SourceText, entry.TranslatedText, entry.FromCache, CreatedAt = createdAt },
            transaction, cancellationToken: cancellationToken));

        if (entry.Timing is { } timing)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO PipelineMetric (HistoryId, CaptureMs, OcrMs, TranslateMs, RenderMs, TotalMs, OcrEngine, CreatedAt) " +
                "VALUES (@HistoryId, @CaptureMs, @OcrMs, @TranslateMs, @RenderMs, @TotalMs, @OcrEngine, @CreatedAt)",
                new
                {
                    HistoryId = historyId,
                    timing.CaptureMs,
                    timing.OcrMs,
                    timing.TranslateMs,
                    timing.RenderMs,
                    timing.TotalMs,
                    timing.OcrEngine,
                    CreatedAt = createdAt,
                },
                transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Các câu mới nhất trước.</summary>
    public async Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<HistoryRow>(new CommandDefinition(
            "SELECT h.CreatedAt, h.ProfileId, h.RegionType, h.SourceText, h.TranslatedText, h.FromCache, " +
            "m.CaptureMs, m.OcrMs, m.TranslateMs, m.RenderMs, m.TotalMs, m.OcrEngine " +
            "FROM TranslationHistory h LEFT JOIN PipelineMetric m ON m.HistoryId = h.Id " +
            "ORDER BY h.Id DESC LIMIT @Limit",
            new { Limit = limit }, cancellationToken: cancellationToken));

        var entries = new List<HistoryEntry>();
        foreach (var row in rows)
        {
            PipelineTiming? timing = row.TotalMs is null
                ? null
                : new PipelineTiming(row.CaptureMs ?? 0, row.OcrMs ?? 0, row.TranslateMs ?? 0, row.RenderMs ?? 0, row.TotalMs.Value, row.OcrEngine ?? string.Empty);
            entries.Add(new HistoryEntry(
                DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture),
                row.ProfileId, row.RegionType, row.SourceText, row.TranslatedText, row.FromCache, timing));
        }

        return entries;
    }

    /// <summary>Giữ N ngày gần nhất (mục 7 tài liệu): xóa lịch sử và số đo cũ hơn mốc này.</summary>
    public async Task DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM TranslationHistory WHERE CreatedAt < @Cutoff", new { Cutoff = Format(cutoff) }, cancellationToken: cancellationToken));
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        if (!_created)
        {
            await connection.ExecuteAsync(new CommandDefinition(CreateTablesSql, cancellationToken: cancellationToken));
            _created = true;
        }

        return connection;
    }

    // Lưu giờ UTC dạng ISO 8601 để so sánh chuỗi (CreatedAt < @Cutoff) đúng thứ tự thời gian.
    private static string Format(DateTimeOffset time)
    {
        return time.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }

    private sealed class HistoryRow
    {
        public string CreatedAt { get; init; } = string.Empty;
        public string ProfileId { get; init; } = string.Empty;
        public string RegionType { get; init; } = string.Empty;
        public string SourceText { get; init; } = string.Empty;
        public string TranslatedText { get; init; } = string.Empty;
        public bool FromCache { get; init; }
        public double? CaptureMs { get; init; }
        public double? OcrMs { get; init; }
        public double? TranslateMs { get; init; }
        public double? RenderMs { get; init; }
        public double? TotalMs { get; init; }
        public string? OcrEngine { get; init; }
    }
}
