using System.Text;

namespace AppTranslator.Core.Text;

public static class TextNormalizer
{
    /// <summary>
    /// Chỉ giữ chữ và số, viết thường. OCR hay sai dấu cách, dấu câu và hoa/thường ("Butyou were" / "But you were"),
    /// nên hai câu giống nhau sau bước này được coi là một câu (khóa cache, so câu trùng).
    /// </summary>
    public static string ForComparison(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char character in text)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
