namespace AppTranslator.Core.Pipeline;

/// <summary>
/// Hai kết quả OCR có phải cùng một câu không, bỏ qua lỗi OCR nhỏ ("Butyou" / "But you", "Nesemir" / "Vesemir").
/// Dùng để không báo lại câu cũ khi nền phía sau phụ đề chuyển động làm vùng phụ đề trông như vừa đổi.
/// </summary>
public static class SentenceComparer
{
    // Khác không quá tỉ lệ ký tự này (trên câu dài hơn) thì coi là cùng câu. Câu khác nhau thật thường khác >50%.
    private const double MaxDifferentCharacterRatio = 0.15;

    public static bool AreSameSentence(string first, string second)
    {
        string a = Normalize(first);
        string b = Normalize(second);
        if (a.Length == 0 || b.Length == 0)
            return a.Length == b.Length;

        int distance = CharacterDistance(a, b);
        return distance <= MaxDifferentCharacterRatio * Math.Max(a.Length, b.Length);
    }

    // Chỉ giữ chữ và số, viết thường: OCR hay sai dấu cách, dấu câu và hoa/thường.
    private static string Normalize(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static int CharacterDistance(string first, string second)
    {
        var previousRow = new int[second.Length + 1];
        var currentRow = new int[second.Length + 1];
        for (int j = 0; j <= second.Length; j++)
            previousRow[j] = j;

        for (int i = 1; i <= first.Length; i++)
        {
            currentRow[0] = i;
            for (int j = 1; j <= second.Length; j++)
            {
                int substitutionCost = first[i - 1] == second[j - 1] ? 0 : 1;
                currentRow[j] = Math.Min(Math.Min(previousRow[j] + 1, currentRow[j - 1] + 1), previousRow[j - 1] + substitutionCost);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[second.Length];
    }
}
