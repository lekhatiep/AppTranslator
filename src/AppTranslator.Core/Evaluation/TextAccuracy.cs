using System.Text;

namespace AppTranslator.Core.Evaluation;

/// <summary>
/// Đo độ đúng của OCR so với bản chép chuẩn (AC-01, AC-03): tỉ lệ từ đúng = 1 − (khoảng cách chỉnh sửa theo từ / số từ chuẩn).
/// Khoảng cách chỉnh sửa đếm số từ phải thêm, xóa hoặc thay để chữ OCR thành chữ chuẩn.
/// </summary>
public static class TextAccuracy
{
    // Trong ground-truth.csv các lựa chọn hội thoại nối bằng '|'; khi chấm coi như dấu cách.
    private const char ChoiceSeparator = '|';

    /// <param name="ignoreCaseAndPunctuation">
    /// true: không phân biệt hoa/thường và bỏ dấu câu (đo khả năng đọc chữ);
    /// false: so đúng từng ký tự (dấu câu ảnh hưởng nghĩa bản dịch, vd "?" và "...").
    /// </param>
    public static double WordAccuracy(string expected, string actual, bool ignoreCaseAndPunctuation)
    {
        var expectedWords = Tokenize(expected, ignoreCaseAndPunctuation);
        var actualWords = Tokenize(actual, ignoreCaseAndPunctuation);

        if (expectedWords.Count == 0)
            return actualWords.Count == 0 ? 1 : 0;

        int distance = EditDistance(expectedWords, actualWords);
        return Math.Max(0, 1 - (double)distance / expectedWords.Count);
    }

    private static List<string> Tokenize(string text, bool ignoreCaseAndPunctuation)
    {
        var words = new List<string>();
        string[] parts = text.Replace(ChoiceSeparator, ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            string word = ignoreCaseAndPunctuation ? KeepLettersAndDigits(part).ToLowerInvariant() : part;
            if (word.Length > 0)
                words.Add(word);
        }

        return words;
    }

    private static string KeepLettersAndDigits(string word)
    {
        var builder = new StringBuilder(word.Length);
        foreach (char character in word)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>Levenshtein theo từ, dùng hai hàng của bảng quy hoạch động.</summary>
    private static int EditDistance(List<string> expected, List<string> actual)
    {
        var previousRow = new int[actual.Count + 1];
        var currentRow = new int[actual.Count + 1];
        for (int j = 0; j <= actual.Count; j++)
            previousRow[j] = j;

        for (int i = 1; i <= expected.Count; i++)
        {
            currentRow[0] = i;
            for (int j = 1; j <= actual.Count; j++)
            {
                int substitutionCost = expected[i - 1] == actual[j - 1] ? 0 : 1;
                int deletion = previousRow[j] + 1;
                int insertion = currentRow[j - 1] + 1;
                int substitution = previousRow[j - 1] + substitutionCost;
                currentRow[j] = Math.Min(Math.Min(deletion, insertion), substitution);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[actual.Count];
    }
}
