using System.Diagnostics;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Layout;
using AppTranslator.Core.Pipeline;
using AppTranslator.Core.Profiles;
using AppTranslator.Imaging;
using AppTranslator.Ocr;
using Microsoft.Extensions.Logging;

namespace AppTranslator.App.Pipeline;

/// <param name="Text">Câu tiếng Anh đã ghép.</param>
/// <param name="OcrDuration">Thời gian tiền xử lý + OCR các khung bỏ phiếu.</param>
/// <param name="Latency">Từ khung đầu tiên thấy câu này tới khi đọc xong (gồm cả thời gian chờ chữ đứng yên).</param>
/// <param name="IsCorrection">Cùng câu đang hiện nhưng đọc lại rõ hơn: thay câu cũ, không phải câu mới.</param>
/// <param name="TextBounds">Khung chữ phụ đề gốc theo tỉ lệ 0–1 của cửa sổ (để overlay đè đúng chỗ), null nếu không biết.</param>
public sealed record SubtitleRead(string Text, TimeSpan OcrDuration, TimeSpan Latency, bool IsCorrection = false, LayoutRect? TextBounds = null);

/// <summary>
/// Đọc phụ đề (FR-04) từ từng khung của <see cref="ReaderLoop"/>: cắt vùng phụ đề → chờ chữ đứng yên → OCR vài khung →
/// chọn kết quả ở giữa. Phần tốn CPU (cắt ảnh, mặt nạ, làm nét) chạy nền; sự kiện được gọi trên luồng của vòng chụp.
/// </summary>
public sealed class SubtitleReader : IFrameProcessor
{
    /// <summary>Cấu hình OCR đang dùng (Gate 1 tạm), ghi kèm số đo độ trễ.</summary>
    public const string OcrEngineName = "WindowsOcr+Sharpen";

    private static readonly TimeSpan StableDuration = TimeSpan.FromMilliseconds(300);

    // Số khung gần nhất đem OCR rồi bỏ phiếu: lỗi ngẫu nhiên của một khung (nhiễu nén) bị loại.
    private const int VoteFrameCount = 3;
    private const int MaxBufferedFrames = 10;

    // Mọi lần đọc của cùng một câu được gom lại để bỏ phiếu chung (tối đa ngần này kết quả).
    // Log 24/11: lần đọc đầu hay sai hơn các lần sau; bỏ phiếu cả kho sửa được 6/20 câu, không làm hỏng câu đúng.
    private const int MaxSentenceVotes = 30;

    // Chỉ sửa câu đang hiện khi câu mới gần các phiếu hơn ít nhất ngần này từ (trung bình mỗi phiếu); không thì hai
    // biến thể ngang phiếu ("But you" / "Butyou") sẽ thay nhau hiện, và ở Stage 5 mỗi lần thay là một lần dịch lại.
    private const double MinCorrectionGain = 0.5;

    // Và câu mới phải được ít nhất ngần này lần đọc ra giống hệt. Log 24/11: nền sáng làm nhiều lần đọc sai mỗi lần một kiểu
    // ("df8rugge", "blithe's") và câu bị sửa qua lại mà không tốt hơn; bản sửa đúng thì luôn lặp lại y hệt. Sửa 21 → 10 lần.
    private const int MinCorrectionCopies = 2;

    private readonly ILogger<SubtitleReader> _logger;
    private readonly List<Frame> _recentCrops = [];
    private readonly List<string> _sentenceVotes = [];
    private WindowsOcrEngine? _ocr;
    private Region? _region;
    private StabilityGate _gate = new(StableDuration);
    private string _lastSentence = string.Empty;

    public SubtitleReader(ILogger<SubtitleReader> logger)
    {
        _logger = logger;
    }

    public event Action<SubtitleRead>? SentenceRead;

    public event Action? SubtitleCleared;

    /// <summary>Bắt đầu đọc vùng này, quên trạng thái cũ. Ném InvalidOperationException nếu Windows thiếu OCR tiếng Anh.</summary>
    public void Start(Region region)
    {
        _ocr ??= new WindowsOcrEngine();
        _region = region;
        _gate = new StabilityGate(StableDuration);
        _recentCrops.Clear();
        _sentenceVotes.Clear();
        _lastSentence = string.Empty;
        _logger.LogInformation("Bắt đầu đọc phụ đề, vùng x={X} y={Y} w={W} h={H}", region.X, region.Y, region.W, region.H);
    }

    /// <summary>Chạy vòng chụp riêng chỉ để đọc phụ đề (công cụ phát lại video). App dùng <see cref="ReaderLoop"/> chung.</summary>
    public Task RunAsync(Func<CancellationToken, Task<Frame?>> captureWindow, Region region, CancellationToken cancellationToken)
    {
        Start(region);
        return ReaderLoop.RunAsync(captureWindow, [this], _logger, cancellationToken);
    }

    public async Task ProcessAsync(Frame frame, CancellationToken cancellationToken)
    {
        if (_region is null)
            return;

        var area = _region.ToPixelRect(frame.Width, frame.Height);
        var (crop, mask) = await Task.Run(() => CropAndMask(frame, area), cancellationToken);
        var change = _gate.Update(mask, frame.CapturedAt);
        _logger.LogTrace("Khung {Time:HH:mm:ss.fff}: lệch {Ratio:F2}, ô chữ {Cells}, {Change}",
            frame.CapturedAt, _gate.LastChangeRatio, mask.TextCellCount, change);

        if (change == SubtitleChange.Changed)
            _recentCrops.Clear();

        if (_recentCrops.Count < MaxBufferedFrames)
            _recentCrops.Add(crop);

        if (change == SubtitleChange.Stable)
        {
            var (read, candidates) = await ReadSentenceAsync(_recentCrops, area, frame.Width, frame.Height, _gate.StillSince, cancellationToken);
            if (read is null)
                return;

            // Nền sau phụ đề chuyển động (nhân vật cử động, con trỏ chuột) làm vùng phụ đề trông như đổi dù câu vẫn vậy:
            // không báo câu mới, mà thêm phiếu cho câu đang hiện và chọn lại trên cả kho.
            if (SentenceComparer.AreSameSentence(read.Text, _lastSentence))
            {
                AddVotes(_sentenceVotes, candidates);
                string best = OcrVote.SelectMedian(_sentenceVotes);
                double gain = OcrVote.AverageDistance(_lastSentence, _sentenceVotes) - OcrVote.AverageDistance(best, _sentenceVotes);
                if (gain < MinCorrectionGain || CountCopies(_sentenceVotes, best) < MinCorrectionCopies)
                {
                    _logger.LogDebug("Trùng câu đang hiện, giữ nguyên ({Count} phiếu)", _sentenceVotes.Count);
                    return;
                }

                _lastSentence = best;
                _logger.LogInformation("Sửa phụ đề: \"{Text}\" ({Count} phiếu)", best, _sentenceVotes.Count);
                SentenceRead?.Invoke(read with { Text = best, IsCorrection = true });
                return;
            }

            _sentenceVotes.Clear();
            AddVotes(_sentenceVotes, candidates);
            _lastSentence = read.Text;
            _logger.LogInformation("Đọc phụ đề: \"{Text}\" | OCR {OcrMs:F0} ms, trễ {LatencyMs:F0} ms",
                read.Text, read.OcrDuration.TotalMilliseconds, read.Latency.TotalMilliseconds);
            SentenceRead?.Invoke(read);
        }
        else if (change == SubtitleChange.Cleared)
        {
            _lastSentence = string.Empty;
            _sentenceVotes.Clear();
            SubtitleCleared?.Invoke();
        }
    }
    internal static (Frame Crop, TextMask Mask) CropAndMask(Frame frame, PixelRect area, int minTextLuma = TextMask.DefaultMinTextLuma)
    {
        var crop = FrameConverter.Crop(frame, area);
        return (crop, TextMask.FromFrame(crop, minTextLuma));
    }

    private static int CountCopies(List<string> votes, string text)
    {
        int count = 0;
        foreach (string vote in votes)
        {
            if (vote == text)
                count++;
        }

        return count;
    }

    private static void AddVotes(List<string> votes, List<string> candidates)
    {
        foreach (string candidate in candidates)
        {
            if (votes.Count < MaxSentenceVotes)
                votes.Add(candidate);
        }
    }

    /// <summary>Read = null nếu OCR không ra chữ. Candidates: các kết quả không rỗng của từng khung.</summary>
    /// <param name="area">Vị trí vùng phụ đề trong khung hình, để đổi khung chữ sang tỉ lệ cửa sổ.</param>
    private async Task<(SubtitleRead? Read, List<string> Candidates)> ReadSentenceAsync(
        List<Frame> recentCrops, PixelRect area, int frameWidth, int frameHeight, DateTimeOffset firstSeenAt, CancellationToken cancellationToken)
    {
        // Lấy các khung cuối: khung đầu của lần đứng yên có thể còn đang chuyển cảnh/hiện dần.
        int start = Math.Max(0, recentCrops.Count - VoteFrameCount);
        var stopwatch = Stopwatch.StartNew();
        var candidates = new List<string>();
        PixelRect? textBounds = null;
        for (int i = start; i < recentCrops.Count; i++)
        {
            var crop = recentCrops[i];
            var sharpened = await Task.Run(() => TextPreprocessor.Apply(crop, PreprocessMode.Sharpen), cancellationToken);
            var result = await _ocr!.RecognizeAsync(sharpened, cancellationToken);
            var sentence = SentenceAssembler.AssembleWithBounds(result.Lines, crop.Width);
            candidates.Add(sentence.Text);

            // Hợp khung của mọi khung đem bỏ phiếu: chắc chắn phủ hết chữ gốc dù mỗi khung đọc ra hơi khác.
            if (sentence.Bounds is { } bounds)
                textBounds = textBounds is null ? bounds : textBounds.Value.Union(bounds);
        }

        string text = OcrVote.SelectMedian(candidates);
        stopwatch.Stop();
        foreach (string candidate in candidates)
            _logger.LogDebug("  ứng viên OCR: \"{Candidate}\"", candidate);

        cancellationToken.ThrowIfCancellationRequested();

        var latency = DateTimeOffset.Now - firstSeenAt;
        if (text.Length == 0)
        {
            // Nền sáng đứng yên nhưng không có chữ đọc được.
            _logger.LogDebug("Vùng phụ đề đứng yên nhưng OCR không ra chữ ({Count} khung)", candidates.Count);
            return (null, candidates);
        }

        candidates.RemoveAll(candidate => candidate.Length == 0);
        LayoutRect? windowBounds = null;
        if (textBounds is { } box)
        {
            windowBounds = new LayoutRect(
                (double)(area.X + box.X) / frameWidth,
                (double)(area.Y + box.Y) / frameHeight,
                (double)box.Width / frameWidth,
                (double)box.Height / frameHeight);
        }

        return (new SubtitleRead(text, stopwatch.Elapsed, latency, TextBounds: windowBounds), candidates);
    }
}
