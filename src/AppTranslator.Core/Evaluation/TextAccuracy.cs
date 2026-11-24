using System.Text;
using AppTranslator.Core.Text;

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

        int distance = WordDistance.Between(expectedWords, actualWords);
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
}
