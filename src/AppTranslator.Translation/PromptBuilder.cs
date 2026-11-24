using System.Text;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Profiles;

namespace AppTranslator.Translation;

/// <summary>
/// Tạo lời dặn (system instruction) và nội dung gửi AI từ hồ sơ game (FR-09).
/// Lời dặn viết tiếng Anh cho AI dễ theo; văn phong/xưng hô lấy nguyên từ hồ sơ (tiếng Việt).
/// </summary>
public static class PromptBuilder
{
    public static string BuildInstruction(GameProfile profile)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"You translate English video game subtitles into Vietnamese for the game \"{profile.GameName}\".");
        // Câu nguồn là kết quả OCR, có thể sai nhẹ (dính từ, sai chữ cái, ký tự lạ): AI tự đoán ý đúng.
        builder.AppendLine("Each English line was read from the screen by OCR and may contain recognition errors: merged or split words, wrong letters, stray symbols.");
        builder.AppendLine("Silently infer the intended English and translate that meaning. Never mention the errors.");

        if (!string.IsNullOrWhiteSpace(profile.Style.Tone))
            builder.AppendLine($"Tone: {profile.Style.Tone}");

        if (!string.IsNullOrWhiteSpace(profile.Style.Addressing))
            builder.AppendLine($"Forms of address: {profile.Style.Addressing}");

        if (profile.KeepNames.Count > 0)
            builder.AppendLine($"Keep these names exactly as written, do not translate them: {string.Join(", ", profile.KeepNames)}");

        if (profile.Glossary.Count > 0)
        {
            builder.AppendLine("Always use these fixed translations:");
            foreach (var entry in profile.Glossary)
                builder.AppendLine($"- {entry.En} => {entry.Vi}");
        }

        builder.AppendLine("Keep the translation about as short as the original so it fits on screen as a subtitle.");
        builder.Append("Reply with only the Vietnamese translation of the current line, on a single line, without quotes, labels or notes.");
        return builder.ToString();
    }

    public static string BuildUserMessage(TranslationRequest request)
    {
        var builder = new StringBuilder();
        if (request.PreviousLines.Count > 0)
        {
            builder.AppendLine("Previous lines, for context only (do not translate them again):");
            foreach (var line in request.PreviousLines)
            {
                builder.AppendLine($"EN: {line.Source}");
                builder.AppendLine($"VI: {line.Translation}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("Current line:");
        builder.Append(request.SourceText);
        return builder.ToString();
    }
}
