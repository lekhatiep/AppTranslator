using System.Text;

namespace AppTranslator.Core.Profiles;

/// <summary>FR-08: đổi qua lại giữa nội dung hồ sơ và chữ người dùng gõ trong màn sửa hồ sơ.</summary>
public static class ProfileText
{
    public const char GlossarySeparator = '=';

    /// <summary>Mỗi dòng một tên; bỏ dòng trống và tên trùng (không phân biệt hoa/thường), giữ thứ tự.</summary>
    public static IReadOnlyList<string> ParseNames(string text)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in SplitLines(text))
        {
            if (seen.Add(line))
                names.Add(line);
        }

        return names;
    }

    public static string FormatNames(IReadOnlyList<string> names)
    {
        return string.Join(Environment.NewLine, names);
    }

    /// <summary>Mỗi dòng "tiếng Anh = tiếng Việt". Dòng sai được ghi vào errors kèm số dòng (đếm từ 1, tính cả dòng trống).</summary>
    public static IReadOnlyList<GlossaryEntry> ParseGlossary(string text, List<string> errors)
    {
        var entries = new List<GlossaryEntry>();
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0)
                continue;

            int separator = line.IndexOf(GlossarySeparator);
            string english = separator < 0 ? string.Empty : line[..separator].Trim();
            string vietnamese = separator < 0 ? string.Empty : line[(separator + 1)..].Trim();
            if (english.Length == 0 || vietnamese.Length == 0)
            {
                errors.Add($"Thuật ngữ dòng {i + 1} phải có dạng \"tiếng Anh {GlossarySeparator} tiếng Việt\": {line}");
                continue;
            }

            entries.Add(new GlossaryEntry(english, vietnamese));
        }

        return entries;
    }

    public static string FormatGlossary(IReadOnlyList<GlossaryEntry> glossary)
    {
        var lines = new List<string>();
        foreach (var entry in glossary)
            lines.Add($"{entry.En} {GlossarySeparator} {entry.Vi}");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Mã hồ sơ (tên file JSON) từ tên game: chữ thường a–z, số, gạch nối; thêm -2, -3... nếu đã có.
    /// Tên toàn ký tự khác (vd tiếng Nhật) thì dùng "game".
    /// </summary>
    public static string CreateId(string gameName, IReadOnlyCollection<string> existingIds)
    {
        var builder = new StringBuilder();
        foreach (char character in gameName.Trim().ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
                builder.Append(character);
            else if (builder.Length > 0 && builder[^1] != '-')
                builder.Append('-');
        }

        string baseId = builder.ToString().Trim('-');
        if (baseId.Length == 0)
            baseId = "game";

        string id = baseId;
        int suffix = 2;
        while (existingIds.Contains(id, StringComparer.OrdinalIgnoreCase))
        {
            id = $"{baseId}-{suffix}";
            suffix++;
        }

        return id;
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0)
                yield return trimmed;
        }
    }
}
