namespace AppTranslator.Core.Pipeline;

public enum SubtitleChange
{
    /// <summary>Không có gì mới (đang chờ đứng yên, hoặc đã báo cho lần đứng yên này rồi).</summary>
    None,

    /// <summary>Chữ vừa đổi so với lúc bắt đầu đứng yên: các khung trước đó không còn dùng được.</summary>
    Changed,

    /// <summary>Chữ đã đứng yên đủ lâu: đọc câu (OCR). Chỉ báo một lần cho mỗi lần đứng yên.</summary>
    Stable,

    /// <summary>Phụ đề đã biến mất và vùng trống đủ lâu: ẩn bản dịch.</summary>
    Cleared,
}

/// <summary>
/// Chỉ cho OCR khi chữ phụ đề đã đứng yên một khoảng thời gian, tránh đọc lúc chữ đang hiện dần hoặc đang chuyển câu.
/// So mỗi khung với khung bắt đầu đứng yên (không phải khung ngay trước), nên thay đổi chậm vẫn bị phát hiện.
/// </summary>
public sealed class StabilityGate
{
    // Khác quá tỉ lệ này (xem TextMask.ChangeRatio) thì coi là chữ đã đổi. Đo trên 14 ảnh mẫu: cùng câu nhưng nén
    // JPEG chất lượng 60 lệch tối đa 0.19; hai câu liền nhau khác nhau ít nhất 0.44.
    private const double ChangeThreshold = 0.3;

    private readonly TimeSpan _stableDuration;
    private TextMask? _reference;
    private DateTimeOffset _stillSince;
    private bool _reported;
    private bool _showingText;

    public StabilityGate(TimeSpan stableDuration)
    {
        _stableDuration = stableDuration;
    }

    /// <summary>Lúc chữ hiện tại bắt đầu đứng yên (≈ lúc câu xuất hiện), dùng để đo độ trễ.</summary>
    public DateTimeOffset StillSince => _stillSince;

    public SubtitleChange Update(TextMask mask, DateTimeOffset capturedAt)
    {
        if (_reference is null || mask.ChangeRatio(_reference) > ChangeThreshold)
        {
            _reference = mask;
            _stillSince = capturedAt;
            _reported = false;
            return SubtitleChange.Changed;
        }

        if (_reported || capturedAt - _stillSince < _stableDuration)
            return SubtitleChange.None;

        _reported = true;
        if (_reference.HasText)
        {
            _showingText = true;
            return SubtitleChange.Stable;
        }

        if (_showingText)
        {
            _showingText = false;
            return SubtitleChange.Cleared;
        }

        return SubtitleChange.None;
    }
}
