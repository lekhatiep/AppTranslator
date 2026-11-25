using System.Globalization;
using System.Windows;
using AppTranslator.Core.Profiles;

namespace AppTranslator.App;

/// <summary>
/// FR-08: tạo hoặc sửa hồ sơ game (tên, văn phong, xưng hô, tên riêng, thuật ngữ). Vùng chữ sửa ở RegionSelectorWindow.
/// Lưu thì tăng Version để cache bản dịch theo phiên bản cũ không còn được dùng.
/// </summary>
public partial class ProfileEditorWindow : Window
{
    private const int MaxContextLines = 10;

    private readonly GameProfile? _existing;
    private readonly IReadOnlyCollection<string> _existingIds;

    /// <param name="existing">Hồ sơ cần sửa, hoặc null để tạo hồ sơ mới.</param>
    /// <param name="existingIds">Mã các hồ sơ đang có, để hồ sơ mới không trùng tên file.</param>
    public ProfileEditorWindow(GameProfile? existing, IReadOnlyCollection<string> existingIds)
    {
        InitializeComponent();
        _existing = existing;
        _existingIds = existingIds;
        Title = existing is null ? "Tạo hồ sơ game" : $"Sửa hồ sơ: {existing.GameName}";

        var profile = existing ?? new GameProfile { Id = string.Empty, GameName = string.Empty };
        GameNameBox.Text = profile.GameName;
        WindowHintBox.Text = profile.WindowTitleHint ?? string.Empty;
        ToneBox.Text = profile.Style.Tone ?? string.Empty;
        AddressingBox.Text = profile.Style.Addressing ?? string.Empty;
        ContextLinesBox.Text = profile.Style.ContextLines.ToString(CultureInfo.InvariantCulture);
        KeepNamesBox.Text = ProfileText.FormatNames(profile.KeepNames);
        GlossaryBox.Text = ProfileText.FormatGlossary(profile.Glossary);
    }

    /// <summary>Hồ sơ đã sửa; chỉ có giá trị khi ShowDialog trả về true.</summary>
    public GameProfile? Result { get; private set; }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var errors = new List<string>();
        string gameName = GameNameBox.Text.Trim();
        if (gameName.Length == 0)
            errors.Add("Chưa nhập tên game.");

        if (!int.TryParse(ContextLinesBox.Text.Trim(), out int contextLines) || contextLines < 0 || contextLines > MaxContextLines)
            errors.Add($"Số câu ngữ cảnh phải là số từ 0 đến {MaxContextLines}.");

        var glossary = ProfileText.ParseGlossary(GlossaryBox.Text, errors);
        if (errors.Count > 0)
        {
            ErrorText.Text = string.Join(Environment.NewLine, errors);
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var baseProfile = _existing ?? new GameProfile
        {
            Id = ProfileText.CreateId(gameName, _existingIds),
            GameName = gameName,
            Version = 0,
        };

        Result = baseProfile with
        {
            GameName = gameName,
            WindowTitleHint = EmptyToNull(WindowHintBox.Text),
            Style = baseProfile.Style with
            {
                Tone = EmptyToNull(ToneBox.Text),
                Addressing = EmptyToNull(AddressingBox.Text),
                ContextLines = contextLines,
            },
            KeepNames = ProfileText.ParseNames(KeepNamesBox.Text),
            Glossary = glossary,
            Version = baseProfile.Version + 1,
        };
        DialogResult = true;
    }

    private static string? EmptyToNull(string text)
    {
        string trimmed = text.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
