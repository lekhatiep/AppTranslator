namespace AppTranslator.Core.Text;

/// <summary>
/// Đoán câu OCR có phải chữ rác không (vd "Viljåble. Sh&amp;uldjl turn backan 11" khi nền sáng sau chữ),
/// để không gửi đi dịch: AI sẽ đoán bừa ra một câu trôi chảy nhưng sai nghĩa.
/// Lỗi nhẹ ("Butyou", "Nesemir", "ÄJgh") vẫn cho qua vì AI tự hiểu được.
/// </summary>
public static class OcrQuality
{
    // Dưới tỉ lệ từ "trông giống từ" này thì coi là rác.
    private const double MinWordLikeRatio = 0.7;

    public static bool LooksLikeGarbage(string text)
    {
        string[] words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return true;

        int wordLike = 0;
        foreach (string word in words)
        {
            if (IsWordLike(word))
                wordLike++;
        }

        return (double)wordLike / words.Length < MinWordLikeRatio;
    }

    /// <summary>
    /// Bỏ ký tự bao quanh ở hai đầu, phần còn lại chỉ gồm chữ cái tiếng Anh (a–z), dấu nháy/gạch nối và dấu câu
    /// (OCR hay dính hai từ qua dấu câu: "Hm?What", "courtyard.She"). Số và ký tự như å, &amp;, @ là dấu hiệu OCR sai.
    /// Không bắt được rác toàn chữ cái ("uldll") vì không dùng từ điển.
    /// </summary>
    private static bool IsWordLike(string word)
    {
        string core = word.Trim('"', '(', ')', '[', ']', '—', '…', '*');
        foreach (char character in core)
        {
            bool isEnglishLetter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
            bool isPunctuation = character is '\'' or '-' or '.' or ',' or '!' or '?' or ':' or ';' or '"';
            if (!isEnglishLetter && !isPunctuation)
                return false;
        }

        return true;
    }
}
