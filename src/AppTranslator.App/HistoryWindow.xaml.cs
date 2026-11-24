using System.Windows;
using AppTranslator.Core.Evaluation;
using AppTranslator.Storage;

namespace AppTranslator.App;

/// <summary>FR-12: xem lại các câu đã dịch gần đây, kèm độ trễ từng bước và p50/p90 (AC-05).</summary>
public partial class HistoryWindow : Window
{
    private const int MaxRows = 200;

    private readonly SqliteHistoryStore _history;

    public HistoryWindow(SqliteHistoryStore history)
    {
        InitializeComponent();
        _history = history;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var entries = await _history.GetRecentAsync(MaxRows);
            var rows = new List<HistoryRow>();
            var totals = new List<double>();
            int cacheHits = 0;
            foreach (var entry in entries)
            {
                rows.Add(new HistoryRow(
                    entry.CreatedAt.LocalDateTime.ToString("HH:mm:ss"),
                    entry.SourceText,
                    entry.TranslatedText,
                    entry.FromCache ? "cache" : "AI",
                    Format(entry.Timing?.OcrMs),
                    Format(entry.Timing?.TranslateMs),
                    Format(entry.Timing?.TotalMs)));

                if (entry.Timing is { } timing)
                    totals.Add(timing.TotalMs);
                if (entry.FromCache)
                    cacheHits++;
            }

            HistoryGrid.ItemsSource = rows;
            SummaryText.Text = BuildSummary(entries.Count, cacheHits, totals);
        }
        catch (Exception exception)
        {
            SummaryText.Text = $"Không đọc được lịch sử: {exception.Message}";
        }
    }

    private static string BuildSummary(int count, int cacheHits, List<double> totals)
    {
        if (count == 0)
            return "Chưa có câu nào được dịch.";

        string summary = $"{count} câu gần nhất, {cacheHits} câu lấy từ cache.";
        var p50 = LatencyStats.Percentile(totals, 50);
        var p90 = LatencyStats.Percentile(totals, 90);
        if (p50 is not null && p90 is not null)
            summary += $" Độ trễ tổng (từ lúc câu hiện tới lúc bản dịch hiện, {totals.Count} câu): p50 {p50:F0} ms, p90 {p90:F0} ms (mục tiêu AC-05: p90 ≤ 1500 ms).";

        return summary;
    }

    private static string Format(double? milliseconds)
    {
        return milliseconds is null ? "" : milliseconds.Value.ToString("F0");
    }

    private sealed record HistoryRow(string Time, string Source, string Translation, string Origin, string OcrMs, string TranslateMs, string TotalMs);
}
