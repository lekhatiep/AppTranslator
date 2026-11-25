using AppTranslator.App.Overlay;
using AppTranslator.App.Pipeline;
using AppTranslator.Core.Layout;
using AppTranslator.Core.Profiles;
using AppTranslator.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace AppTranslator.App;

/// <summary>Phần lựa chọn hội thoại (FR-05): dịch bảng lựa chọn, hiện lại số thứ tự, ▶ và màu nổi bật.</summary>
public partial class MainViewModel
{
    private IReadOnlyList<ChoiceItem> _choiceItems = [];
    private IReadOnlyList<string> _choiceTranslations = [];
    private int? _selectedChoice;
    private OverlayText? _overlayChoices;

    [ObservableProperty]
    private string _lastChoices = string.Empty;

    private void OnChoicesRead(ChoicesRead read)
    {
        // Bảng mới: ẩn bản dịch của bảng cũ cho tới khi dịch xong bảng này.
        _choiceItems = read.Choices;
        _choiceTranslations = [];
        _selectedChoice = read.Selected;
        ShowChoices();
        _ = TranslateChoicesAsync(read);
    }

    private async Task TranslateChoicesAsync(ChoicesRead read)
    {
        var profile = SelectedProfile;
        if (profile is null)
            return;

        var texts = new List<string>();
        foreach (var choice in read.Choices)
            texts.Add(choice.Text);

        try
        {
            var result = await _subtitleTranslator.TranslateChoicesAsync(profile, texts);
            if (result is null)
                return;

            if (result.Error is not null)
            {
                // FR-13: báo lỗi ngay trên bảng lựa chọn, giữ nguyên chữ gốc để người chơi vẫn chọn được.
                var withError = new List<string> { $"⚠ {result.Error.Message}" };
                withError.AddRange(texts);
                _choiceTranslations = withError;
                ShowChoices(errorLine: true);
                return;
            }

            var translations = new List<string>();
            for (int i = 0; i < texts.Count; i++)
                translations.Add(result.Lines[i]?.TranslatedText ?? texts[i]);

            _choiceTranslations = translations;
            ShowChoices();

            for (int i = 0; i < texts.Count; i++)
            {
                if (result.Lines[i] is { } line)
                {
                    await _history.AddAsync(new HistoryEntry(
                        DateTimeOffset.Now, profile.Id, nameof(RegionType.Choices), line.SourceText, line.TranslatedText, line.FromCache, null));
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Dịch lựa chọn thất bại");
            LastChoices = $"Lỗi dịch lựa chọn: {exception.Message}";
        }
    }

    private void OnChoiceSelectionChanged(int index)
    {
        // Chỉ di chuyển ▶: vẽ lại, không OCR và không dịch lại.
        _selectedChoice = index;
        ShowChoices();
    }

    private void ClearChoices()
    {
        _subtitleTranslator.ResetChoices();
        _choiceItems = [];
        _choiceTranslations = [];
        _selectedChoice = null;
        LastChoices = string.Empty;
        ShowChoices();
    }

    /// <param name="errorLine">Dòng đầu là thông báo lỗi, các dòng sau là chữ gốc.</param>
    private void ShowChoices(bool errorLine = false)
    {
        var region = SelectedProfile?.Regions.FirstOrDefault(r => r.Type == RegionType.Choices);
        if (region is null || _choiceItems.Count == 0 || _choiceTranslations.Count == 0)
        {
            _overlayChoices = null;
            RefreshOverlay();
            return;
        }

        int offset = errorLine ? 1 : 0;
        var lines = new List<string>();
        if (errorLine)
            lines.Add(_choiceTranslations[0]);

        var summary = new List<string>();
        for (int i = 0; i < _choiceItems.Count; i++)
        {
            var item = _choiceItems[i];
            string number = $"{item.Number}. ";
            string marker = i == _selectedChoice ? "▶ " : "    ";
            string translation = _choiceTranslations[i + offset];
            lines.Add($"{marker}{number}{translation}");
            summary.Add($"{(i == _selectedChoice ? "▶ " : "")}{number}{item.Text} → {translation}");
        }

        int? highlighted = _selectedChoice is int selected ? selected + offset : null;
        _overlayChoices = new OverlayText(region, string.Join("\n", lines), UnionOf(_choiceItems), highlighted);
        LastChoices = string.Join(Environment.NewLine, summary);
        RefreshOverlay();
    }

    private static LayoutRect UnionOf(IReadOnlyList<ChoiceItem> items)
    {
        double left = double.MaxValue;
        double top = double.MaxValue;
        double right = double.MinValue;
        double bottom = double.MinValue;
        foreach (var item in items)
        {
            left = Math.Min(left, item.Bounds.X);
            top = Math.Min(top, item.Bounds.Y);
            right = Math.Max(right, item.Bounds.Right);
            bottom = Math.Max(bottom, item.Bounds.Bottom);
        }

        return new LayoutRect(left, top, right - left, bottom - top);
    }
}
