using AppTranslator.Core.Abstractions;

namespace AppTranslator.Core.Pipeline;

/// <summary>
/// FR-04: ghép các dòng OCR của vùng phụ đề thành một câu.
/// Phụ đề luôn căn giữa màn hình, còn chữ HUD (hướng dẫn phím...) nằm lệch sang một bên,
/// nên chỉ giữ những hàng chữ có tâm gần tâm vùng.
/// </summary>
public static class SentenceAssembler
{
    // Tâm hàng chữ lệch khỏi tâm vùng quá tỉ lệ này (so với bề rộng vùng) thì coi là HUD.
    // Ảnh mẫu: phụ đề lệch < 5%, HUD "Sprint Left Shift" lệch ~40%.
    private const double MaxCenterOffsetRatio = 0.15;

    // Hai mảnh OCR thuộc cùng một hàng nếu phần chồng nhau theo chiều dọc ≥ tỉ lệ này của mảnh thấp hơn.
    private const double SameRowOverlapRatio = 0.5;

    public static string Assemble(IReadOnlyList<OcrLine> lines, int regionWidth)
    {
        var rows = GroupIntoRows(lines);
        var keptTexts = new List<string>();
        foreach (var row in rows)
        {
            if (IsCentered(row.Bounds, regionWidth))
                keptTexts.Add(row.Text);
        }

        return NormalizeSpaces(string.Join(" ", keptTexts));
    }

    /// <summary>Gộp các mảnh cùng hàng (trái sang phải), trả về các hàng theo thứ tự trên xuống. Dùng chung với ChoiceParser.</summary>
    internal static List<OcrLine> GroupIntoRows(IReadOnlyList<OcrLine> lines)
    {
        var sortedByTop = new List<OcrLine>(lines);
        sortedByTop.Sort((first, second) => first.Bounds.Y.CompareTo(second.Bounds.Y));

        var rows = new List<List<OcrLine>>();
        foreach (var line in sortedByTop)
        {
            var row = FindRow(rows, line);
            if (row is null)
                rows.Add([line]);
            else
                row.Add(line);
        }

        var result = new List<OcrLine>();
        foreach (var row in rows)
            result.Add(MergeRow(row));

        return result;
    }

    private static List<OcrLine>? FindRow(List<List<OcrLine>> rows, OcrLine line)
    {
        foreach (var row in rows)
        {
            if (OverlapsVertically(row[0].Bounds, line.Bounds))
                return row;
        }

        return null;
    }

    private static bool OverlapsVertically(PixelRect first, PixelRect second)
    {
        int overlap = Math.Min(first.Y + first.Height, second.Y + second.Height) - Math.Max(first.Y, second.Y);
        int smallerHeight = Math.Min(first.Height, second.Height);
        return smallerHeight > 0 && overlap >= smallerHeight * SameRowOverlapRatio;
    }

    private static OcrLine MergeRow(List<OcrLine> row)
    {
        row.Sort((first, second) => first.Bounds.X.CompareTo(second.Bounds.X));

        int left = int.MaxValue;
        int top = int.MaxValue;
        int right = int.MinValue;
        int bottom = int.MinValue;
        var texts = new List<string>();
        foreach (var part in row)
        {
            left = Math.Min(left, part.Bounds.X);
            top = Math.Min(top, part.Bounds.Y);
            right = Math.Max(right, part.Bounds.X + part.Bounds.Width);
            bottom = Math.Max(bottom, part.Bounds.Y + part.Bounds.Height);
            texts.Add(part.Text);
        }

        var bounds = new PixelRect(left, top, right - left, bottom - top);
        return new OcrLine(string.Join(" ", texts), bounds, Confidence: null);
    }

    private static bool IsCentered(PixelRect bounds, int regionWidth)
    {
        double rowCenter = bounds.X + bounds.Width / 2.0;
        double regionCenter = regionWidth / 2.0;
        return Math.Abs(rowCenter - regionCenter) <= regionWidth * MaxCenterOffsetRatio;
    }

    private static string NormalizeSpaces(string text)
    {
        string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words);
    }
}
