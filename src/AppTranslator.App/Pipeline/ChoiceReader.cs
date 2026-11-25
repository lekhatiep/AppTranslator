using System.Diagnostics;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Layout;
using AppTranslator.Core.Pipeline;
using AppTranslator.Core.Profiles;
using AppTranslator.Ocr;
using Microsoft.Extensions.Logging;

namespace AppTranslator.App.Pipeline;

/// <param name="Bounds">Khung dòng lựa chọn theo tỉ lệ 0–1 của cửa sổ.</param>
public sealed record ChoiceItem(int Number, string Text, LayoutRect Bounds);

/// <param name="Selected">Chỉ số lựa chọn đang có ▶, null nếu không nhận ra.</param>
public sealed record ChoicesRead(IReadOnlyList<ChoiceItem> Choices, int? Selected, TimeSpan OcrDuration);

/// <summary>
/// Đọc lựa chọn hội thoại (FR-05) từ từng khung của <see cref="ReaderLoop"/>: vùng lựa chọn đổi rồi đứng yên → OCR
/// (PaddleOCR, Gate 1) → tách lựa chọn. Mỗi khung dò lại ▶ (rẻ) để bám khi người chơi di chuyển lựa chọn mà không OCR lại.
/// </summary>
public sealed class ChoiceReader : IFrameProcessor
{
    public const string OcrEngineName = "PaddleOcrV5";

    private static readonly TimeSpan StableDuration = TimeSpan.FromMilliseconds(300);

    // Chữ lựa chọn vàng sẫm: điểm sáng nhất ~164–169 trên 9 ảnh mẫu (phụ đề trắng dùng 200). Ngưỡng 200 làm bảng lựa chọn
    // ở cảnh tối gần như "không có chữ" và không bao giờ được đọc.
    private const int ChoiceTextMinLuma = 150;

    private readonly ILogger<ChoiceReader> _logger;
    private RapidOcrEngine? _ocr;
    private Region? _region;
    private StabilityGate _gate = new(StableDuration);
    private List<DialogueChoice> _choices = [];
    private int? _selected;

    public ChoiceReader(ILogger<ChoiceReader> logger)
    {
        _logger = logger;
    }

    /// <summary>Bộ lựa chọn mới (nội dung khác bộ đang hiện).</summary>
    public event Action<ChoicesRead>? ChoicesRead;

    /// <summary>Người chơi di chuyển ▶ trong bộ lựa chọn đang hiện.</summary>
    public event Action<int>? SelectionChanged;

    /// <summary>Bảng lựa chọn đã biến mất.</summary>
    public event Action? ChoicesCleared;

    public void Start(Region region)
    {
        _region = region;
        _gate = new StabilityGate(StableDuration);
        _choices = [];
        _selected = null;
        _logger.LogInformation("Bắt đầu đọc lựa chọn, vùng x={X} y={Y} w={W} h={H}", region.X, region.Y, region.W, region.H);
    }

    public async Task ProcessAsync(Frame frame, CancellationToken cancellationToken)
    {
        if (_region is null)
            return;

        var area = _region.ToPixelRect(frame.Width, frame.Height);
        var (crop, mask) = await Task.Run(() => SubtitleReader.CropAndMask(frame, area, ChoiceTextMinLuma), cancellationToken);
        var change = _gate.Update(mask, frame.CapturedAt);
        _logger.LogTrace("Lựa chọn, khung {Time:HH:mm:ss.fff}: lệch {Ratio:F2}, ô chữ {Cells}, {Change}",
            frame.CapturedAt, _gate.LastChangeRatio, mask.TextCellCount, change);

        if (_choices.Count > 0)
            TrackSelection(crop);

        if (change == SubtitleChange.Stable)
            await ReadChoicesAsync(crop, area, frame.Width, frame.Height, cancellationToken);
        else if (change == SubtitleChange.Cleared)
            Clear();
    }

    private void TrackSelection(Frame crop)
    {
        var rows = new List<PixelRect>();
        foreach (var choice in _choices)
            rows.Add(choice.Bounds);

        int? selected = SelectionMarker.FindSelected(crop, rows);
        if (selected is null || selected == _selected)
            return;

        _selected = selected;
        _logger.LogDebug("Lựa chọn đang chọn: {Index}", selected.Value + 1);
        SelectionChanged?.Invoke(selected.Value);
    }

    private async Task ReadChoicesAsync(Frame crop, PixelRect area, int frameWidth, int frameHeight, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        // Nạp model ONNX (~1 s) và chạy PaddleOCR đều tốn CPU: chạy nền để không đứng giao diện.
        _ocr ??= await Task.Run(() => new RapidOcrEngine(), cancellationToken);
        var ocr = _ocr;
        var result = await Task.Run(() => ocr.RecognizeAsync(crop, cancellationToken), cancellationToken);
        var choices = ChoiceParser.Parse(result.Lines);
        stopwatch.Stop();
        cancellationToken.ThrowIfCancellationRequested();

        if (choices.Count == 0)
        {
            // Vùng đổi vì bảng lựa chọn biến mất (nền sáng làm mặt nạ không bao giờ "trống").
            Clear();
            return;
        }

        var rows = new List<PixelRect>();
        foreach (var choice in choices)
            rows.Add(choice.Bounds);
        int? selected = SelectionMarker.FindSelected(crop, rows);

        if (HaveSameTexts(choices, _choices))
        {
            // Vẫn bộ lựa chọn cũ (vùng đổi vì ▶ di chuyển hoặc nền chuyển động): chỉ cập nhật khung và ▶.
            _choices = [.. choices];
            if (selected is not null && selected != _selected)
            {
                _selected = selected;
                SelectionChanged?.Invoke(selected.Value);
            }

            return;
        }

        _choices = [.. choices];
        _selected = selected;
        var items = new List<ChoiceItem>();
        foreach (var choice in choices)
        {
            var bounds = new LayoutRect(
                (double)(area.X + choice.Bounds.X) / frameWidth,
                (double)(area.Y + choice.Bounds.Y) / frameHeight,
                (double)choice.Bounds.Width / frameWidth,
                (double)choice.Bounds.Height / frameHeight);
            items.Add(new ChoiceItem(choice.Number, choice.Text, bounds));
        }

        _logger.LogInformation("Đọc lựa chọn ({Count}, đang chọn {Selected}) | OCR {Milliseconds:F0} ms: {Texts}",
            items.Count, selected is null ? "?" : (selected.Value + 1).ToString(), stopwatch.Elapsed.TotalMilliseconds,
            string.Join(" | ", items.Select(item => item.Text)));
        ChoicesRead?.Invoke(new ChoicesRead(items, selected, stopwatch.Elapsed));
    }

    private void Clear()
    {
        if (_choices.Count == 0)
            return;

        _choices = [];
        _selected = null;
        _logger.LogInformation("Bảng lựa chọn đã tắt");
        ChoicesCleared?.Invoke();
    }

    private static bool HaveSameTexts(IReadOnlyList<DialogueChoice> first, IReadOnlyList<DialogueChoice> second)
    {
        if (first.Count != second.Count)
            return false;

        for (int i = 0; i < first.Count; i++)
        {
            if (!SentenceComparer.AreSameSentence(first[i].Text, second[i].Text))
                return false;
        }

        return true;
    }
}
