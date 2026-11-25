namespace AppTranslator.Core.Evaluation;

/// <param name="Passed">Số câu khách chấm "Đạt".</param>
/// <param name="Graded">Số câu đã chấm (Đạt + Sai).</param>
/// <param name="Total">Tổng số câu trong bảng (kể cả chưa chấm).</param>
public sealed record ReviewScore(int Passed, int Graded, int Total)
{
    /// <summary>Tỉ lệ đạt trên số câu đã chấm; null nếu chưa chấm câu nào.</summary>
    public double? PassRate => Graded == 0 ? null : (double)Passed / Graded;
}

/// <summary>
/// AC-02 / AC-04: khách chấm từng bản dịch "Đạt" hoặc "Sai" (sai nghĩa) trong bảng xuất từ lịch sử; tính tỉ lệ đạt theo loại vùng.
/// </summary>
public static class TranslationReview
{
    public const string PassVerdict = "Đạt";
    public const string FailVerdict = "Sai";

    /// <summary>Chấp nhận có/không dấu, hoa/thường: "Đạt", "dat", "Sai", "SAI". Ô trống hay giá trị khác là chưa chấm (null).</summary>
    public static bool? ParseVerdict(string? verdict)
    {
        string value = (verdict ?? string.Empty).Trim().ToLowerInvariant();
        if (value is "đạt" or "dat" or "đat" or "dạt")
            return true;
        if (value == "sai")
            return false;

        return null;
    }

    /// <summary>Điểm theo từng loại vùng (Subtitle, Choices, Manual...).</summary>
    public static IReadOnlyDictionary<string, ReviewScore> Score(IEnumerable<(string RegionType, string? Verdict)> rows)
    {
        var passed = new Dictionary<string, int>();
        var graded = new Dictionary<string, int>();
        var total = new Dictionary<string, int>();
        foreach (var (regionType, verdict) in rows)
        {
            total[regionType] = total.GetValueOrDefault(regionType) + 1;
            bool? result = ParseVerdict(verdict);
            if (result is null)
                continue;

            graded[regionType] = graded.GetValueOrDefault(regionType) + 1;
            if (result.Value)
                passed[regionType] = passed.GetValueOrDefault(regionType) + 1;
        }

        var scores = new Dictionary<string, ReviewScore>();
        foreach (var (regionType, count) in total)
            scores[regionType] = new ReviewScore(passed.GetValueOrDefault(regionType), graded.GetValueOrDefault(regionType), count);

        return scores;
    }
}
