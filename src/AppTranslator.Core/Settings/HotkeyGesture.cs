namespace AppTranslator.Core.Settings;

/// <summary>Phím bổ trợ, giá trị trùng với MOD_* của Win32 RegisterHotKey.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Ctrl = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>Phím tắt đọc từ chuỗi trong settings.json, ví dụ "Ctrl+Alt+T" hoặc "Ctrl+Shift+F2".</summary>
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey)
{
    // Mã phím ảo Win32: chữ A–Z và số 0–9 trùng mã ASCII, F1 = 0x70.
    private const int VirtualKeyF1 = 0x70;
    private const int MaxFunctionKey = 24;

    /// <summary>Đọc chuỗi phím tắt; trả về null nếu sai định dạng hoặc thiếu phím bổ trợ.</summary>
    public static HotkeyGesture? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        string[] parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;

        var modifiers = HotkeyModifiers.None;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (!Enum.TryParse(parts[i], ignoreCase: true, out HotkeyModifiers modifier) || modifier == HotkeyModifiers.None)
                return null;

            modifiers |= modifier;
        }

        int? virtualKey = ParseKey(parts[parts.Length - 1]);
        if (virtualKey is null)
            return null;

        return new HotkeyGesture(modifiers, virtualKey.Value);
    }

    private static int? ParseKey(string key)
    {
        string upper = key.ToUpperInvariant();
        if (upper.Length == 1 && char.IsAsciiLetterOrDigit(upper[0]))
            return upper[0];

        if (upper.Length < 2 || upper[0] != 'F')
            return null;

        if (!int.TryParse(upper.Substring(1), out int number) || number < 1 || number > MaxFunctionKey)
            return null;

        return VirtualKeyF1 + number - 1;
    }
}
