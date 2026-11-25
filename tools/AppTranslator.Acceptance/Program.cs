using System.Globalization;
using System.Text;
using AppTranslator.Core.Evaluation;
using AppTranslator.Storage;
using Microsoft.VisualBasic.FileIO;

// Đo nghiệm thu phần dịch (mục 4 tài liệu) từ lịch sử dịch của app (data\translator.db):
//   AppTranslator.Acceptance export [file.csv] [số ngày = 7]
//       Xuất các câu đã dịch ra CSV có cột "danh_gia" để khách điền Đạt / Sai (AC-02 phụ đề, AC-04 lựa chọn),
//       và in độ trễ phụ đề p50/p90 (AC-05).
//   AppTranslator.Acceptance score <file.csv>
//       Đọc bảng khách đã chấm, tính tỉ lệ Đạt theo loại vùng.

Console.OutputEncoding = Encoding.UTF8;
if (args.Length == 0 || (args[0] != "export" && args[0] != "score"))
{
    Console.WriteLine("Cách dùng: AppTranslator.Acceptance export [file.csv] [số ngày = 7]");
    Console.WriteLine("           AppTranslator.Acceptance score <file.csv>");
    return 1;
}

if (args[0] == "export")
{
    string csvPath = args.Length > 1 ? args[1] : $"cham-dich-{DateTime.Now:yyyyMMdd-HHmm}.csv";
    int days = args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 7;
    await ExportAsync(csvPath, days);
    return 0;
}

if (args.Length < 2)
{
    Console.WriteLine("Thiếu đường dẫn file CSV đã chấm.");
    return 1;
}

Score(args[1]);
return 0;

async Task ExportAsync(string csvPath, int days)
{
    var history = new SqliteHistoryStore(AppPaths.Default.DatabaseFile);
    var cutoff = DateTimeOffset.Now.AddDays(-days);
    // Lịch sử chỉ giữ 7 ngày nên lấy hết rồi lọc theo ngày.
    var entries = (await history.GetRecentAsync(int.MaxValue)).Where(entry => entry.CreatedAt >= cutoff).Reverse().ToList();

    // Câu lấy từ cache là câu lặp lại: chỉ chấm một lần mỗi cặp (vùng, câu gốc, bản dịch).
    var seen = new HashSet<(string, string, string)>();
    var builder = new StringBuilder();
    builder.AppendLine("thoi_gian,loai_vung,tieng_anh,ban_dich,danh_gia,ghi_chu");
    int rowCount = 0;
    foreach (var entry in entries)
    {
        if (!seen.Add((entry.RegionType, entry.SourceText, entry.TranslatedText)))
            continue;

        builder.Append(entry.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
            .Append(entry.RegionType).Append(',')
            .Append(Quote(entry.SourceText)).Append(',')
            .Append(Quote(entry.TranslatedText)).AppendLine(",,");
        rowCount++;
    }

    // BOM để Excel mở đúng tiếng Việt.
    File.WriteAllText(csvPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    Console.WriteLine($"Đã xuất {rowCount} câu ({days} ngày gần nhất) ra {Path.GetFullPath(csvPath)}");
    Console.WriteLine($"Khách điền cột danh_gia: \"{TranslationReview.PassVerdict}\" hoặc \"{TranslationReview.FailVerdict}\" (sai nghĩa), rồi chạy: score <file>");

    // AC-05: chỉ câu phụ đề có số đo (câu đọc mới, không tính câu sửa lại).
    var totals = entries.Where(entry => entry.RegionType == "Subtitle" && entry.Timing is not null)
        .Select(entry => entry.Timing!.TotalMs).ToList();
    var p50 = LatencyStats.Percentile(totals, 50);
    var p90 = LatencyStats.Percentile(totals, 90);
    if (p90 is null)
    {
        Console.WriteLine("AC-05: chưa có số đo độ trễ phụ đề.");
        return;
    }

    int fromCache = entries.Count(entry => entry.RegionType == "Subtitle" && entry.Timing is not null && entry.FromCache);
    string verdict = p90 <= 1500 ? "ĐẠT" : "CHƯA ĐẠT";
    Console.WriteLine($"AC-05 độ trễ phụ đề ({totals.Count} câu, {fromCache} từ cache): p50 {p50:F0} ms, p90 {p90:F0} ms — mục tiêu p90 ≤ 1500 ms → {verdict}");
    Console.WriteLine("  (tính từ khung đầu tiên thấy câu, gồm cả 300 ms chờ chữ đứng yên: chặt hơn AC-05 vốn tính từ lúc chữ ổn định)");

    // Câu từ cache gần như tức thì; xem lại cùng đoạn phim nhiều lần làm p90 đẹp hơn thực tế khi chơi lần đầu.
    var aiTotals = entries.Where(entry => entry.RegionType == "Subtitle" && entry.Timing is not null && !entry.FromCache)
        .Select(entry => entry.Timing!.TotalMs).ToList();
    var aiP90 = LatencyStats.Percentile(aiTotals, 90);
    if (aiP90 is not null)
        Console.WriteLine($"  Riêng câu phải gọi AI ({aiTotals.Count} câu, như khi gặp câu lần đầu): p50 {LatencyStats.Percentile(aiTotals, 50):F0} ms, p90 {aiP90:F0} ms");
}

void Score(string csvPath)
{
    using var parser = new TextFieldParser(csvPath, Encoding.UTF8) { HasFieldsEnclosedInQuotes = true };
    parser.SetDelimiters(",");
    parser.ReadFields(); // dòng tiêu đề

    var rows = new List<(string RegionType, string? Verdict)>();
    while (!parser.EndOfData)
    {
        string[] fields = parser.ReadFields() ?? [];
        if (fields.Length < 5)
            continue;

        rows.Add((fields[1], fields[4]));
    }

    var targets = new Dictionary<string, (string Name, double Target)>
    {
        ["Subtitle"] = ("AC-02 dịch phụ đề không sai nghĩa", 0.95),
        ["Choices"] = ("AC-04 dịch lựa chọn không sai nghĩa", 0.90),
    };
    foreach (var (regionType, score) in TranslationReview.Score(rows))
    {
        string ungraded = score.Graded < score.Total ? $", {score.Total - score.Graded} câu chưa chấm" : string.Empty;
        if (score.PassRate is not double rate)
        {
            Console.WriteLine($"{regionType}: chưa chấm câu nào ({score.Total} câu)");
            continue;
        }

        if (targets.TryGetValue(regionType, out var target))
        {
            string verdict = rate >= target.Target ? "ĐẠT" : "CHƯA ĐẠT";
            Console.WriteLine($"{target.Name}: {score.Passed}/{score.Graded} = {rate:P1}{ungraded} — mục tiêu ≥ {target.Target:P0} → {verdict}");
        }
        else
        {
            Console.WriteLine($"{regionType} (tham khảo, không có mục tiêu): {score.Passed}/{score.Graded} = {rate:P1}{ungraded}");
        }
    }
}

static string Quote(string text)
{
    return "\"" + text.Replace("\"", "\"\"") + "\"";
}
