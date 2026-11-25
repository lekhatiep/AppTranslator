using System.Text.RegularExpressions;

namespace AppTranslator.Core.Settings;

/// <summary>FR-11: kiểm tra cài đặt người dùng nhập trước khi lưu; trả về danh sách lỗi tiếng Việt (rỗng = hợp lệ).</summary>
public static partial class SettingsValidator
{
    public const double MinFontSize = 12;
    public const double MaxFontSize = 72;

    // Dưới mức này nền quá trong, phụ đề tiếng Anh gốc lộ ra chồng lên chữ dịch.
    public const double MinBackgroundOpacity = 0.3;

    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        var errors = new List<string>();

        if (settings.Overlay.FontSize < MinFontSize || settings.Overlay.FontSize > MaxFontSize)
            errors.Add($"Cỡ chữ phải từ {MinFontSize} đến {MaxFontSize}.");

        if (!HexColor().IsMatch(settings.Overlay.BackgroundColor))
            errors.Add("Màu nền phải có dạng #RRGGBB, vd #000000.");

        if (settings.Overlay.BackgroundOpacity < MinBackgroundOpacity || settings.Overlay.BackgroundOpacity > 1)
            errors.Add($"Độ mờ nền phải từ {MinBackgroundOpacity} đến 1.");

        if (string.IsNullOrWhiteSpace(settings.GeminiModel))
            errors.Add("Chưa nhập tên model Gemini.");

        var hotkeys = new (string Name, string Text)[]
        {
            ("Bật/tắt dịch", settings.Hotkeys.ToggleTranslation),
            ("Chọn vùng chữ", settings.Hotkeys.SelectRegion),
            ("Khoanh vùng dịch", settings.Hotkeys.TranslateArea),
        };
        var seen = new Dictionary<HotkeyGesture, string>();
        foreach (var (name, text) in hotkeys)
        {
            var gesture = HotkeyGesture.TryParse(text);
            if (gesture is null)
            {
                errors.Add($"Phím tắt \"{name}\" sai định dạng (vd Ctrl+Alt+T).");
                continue;
            }

            if (seen.TryGetValue(gesture, out string? other))
                errors.Add($"Phím tắt \"{name}\" trùng với \"{other}\".");
            else
                seen[gesture] = name;
        }

        return errors;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();
}
