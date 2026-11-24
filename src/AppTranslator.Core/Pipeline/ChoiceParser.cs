using System.Text.RegularExpressions;
using AppTranslator.Core.Abstractions;

namespace AppTranslator.Core.Pipeline;

/// <param name="Number">Số thứ tự trên màn hình (null nếu OCR không đọc ra số).</param>
/// <param name="Text">Nội dung lựa chọn, đã bỏ số thứ tự và ký hiệu ▶.</param>
/// <param name="Bounds">Khung dòng chữ trong ảnh vùng lựa chọn (pixel), gồm cả số thứ tự.</param>
public sealed record DialogueChoice(int? Number, string Text, PixelRect Bounds);

/// <summary>
/// FR-05: tách các lựa chọn hội thoại từ kết quả OCR vùng lựa chọn. Mỗi lựa chọn bắt đầu bằng "số." ("1. Take it…");
/// dòng không có số là phần xuống dòng của lựa chọn ngay trên. Ký hiệu ▶ hay rác OCR trước số bị bỏ.
/// </summary>
public static partial class ChoiceParser
{
    public static IReadOnlyList<DialogueChoice> Parse(IReadOnlyList<OcrLine> lines)
    {
        var choices = new List<DialogueChoice>();
        foreach (var row in SentenceAssembler.GroupIntoRows(lines))
        {
            string text = NormalizeSpaces(row.Text);
            if (text.Length == 0)
                continue;

            var match = NumberedLine().Match(text);
            if (match.Success)
            {
                choices.Add(new DialogueChoice(int.Parse(match.Groups["number"].Value), match.Groups["text"].Value.Trim(), row.Bounds));
            }
            else if (choices.Count > 0)
            {
                var previous = choices[^1];
                choices[^1] = previous with { Text = $"{previous.Text} {text}", Bounds = Union(previous.Bounds, row.Bounds) };
            }
            else
            {
                choices.Add(new DialogueChoice(null, text, row.Bounds));
            }
        }

        return choices;
    }

    // Rác không phải chữ/số trước số thứ tự (▶ đọc thành "»", ">"...), số 1–2 chữ số, dấu chấm (OCR có thể ra "," ")" ":").
    [GeneratedRegex(@"^[^\p{L}\d]*(?<number>\d{1,2})\s*[.,:)]\s*(?<text>.+)$")]
    private static partial Regex NumberedLine();

    private static string NormalizeSpaces(string text)
    {
        return string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static PixelRect Union(PixelRect first, PixelRect second)
    {
        int left = Math.Min(first.X, second.X);
        int top = Math.Min(first.Y, second.Y);
        int right = Math.Max(first.X + first.Width, second.X + second.Width);
        int bottom = Math.Max(first.Y + first.Height, second.Y + second.Height);
        return new PixelRect(left, top, right - left, bottom - top);
    }
}
