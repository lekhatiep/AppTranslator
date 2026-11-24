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
/// Vòng đọc phụ đề (FR-04): chụp ~10 khung/giây → cắt vùng phụ đề → chờ chữ đứng yên → OCR vài khung → chọn kết quả ở giữa.
/// Gọi chụp trên luồng đã gọi RunAsync (luồng UI, giống các nút chụp khác) để hai engine chụp không bị gọi từ hai luồng;
/// phần tốn CPU (cắt ảnh, mặt nạ, làm nét) chạy nền. Sự kiện được gọi trên luồng đã gọi RunAsync.
/// </summary>
public sealed class SubtitleReader
{
    /// <summary>Cấu hình OCR đang dùng (Gate 1 tạm), ghi kèm số đo độ trễ.</summary>
    public const string OcrEngineName = "WindowsOcr+Sharpen";

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(100);
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

    // Cứ ngần này khung thì ghi log thời gian xử lý trung bình mỗi khung (đo tải của vòng chụp).
    private const int TimingLogInterval = 100;

    private readonly ILogger<SubtitleReader> _logger;
    private WindowsOcrEngine? _ocr;

    public SubtitleReader(ILogger<SubtitleReader> logger)
    {
        _logger = logger;
    }

    public event Action<SubtitleRead>? SentenceRead;

    public event Action? SubtitleCleared;

    /// <summary>Chạy tới khi bị hủy (ném OperationCanceledException). Ném InvalidOperationException nếu Windows thiếu OCR tiếng Anh.</summary>
    public async Task RunAsync(Func<CancellationToken, Task<Frame?>> captureWindow, Region region, CancellationToken cancellationToken)
    {
        _ocr ??= new WindowsOcrEngine();

        var gate = new StabilityGate(StableDuration);
        var recentCrops = new List<Frame>();
        var tickStopwatch = new Stopwatch();
        int tickCount = 0;
        string lastSentence = string.Empty;
        var sentenceVotes = new List<string>();
        using var timer = new PeriodicTimer(FrameInterval);

        _logger.LogInformation("Bắt đầu đọc phụ đề, vùng x={X} y={Y} w={W} h={H}", region.X, region.Y, region.W, region.H);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            tickStopwatch.Start();
            var frame = await captureWindow(cancellationToken);
            if (frame is null)
            {
                tickStopwatch.Stop();
                continue;
            }

            var area = region.ToPixelRect(frame.Width, frame.Height);
            var (crop, mask) = await Task.Run(() => CropAndMask(frame, area), cancellationToken);
            var change = gate.Update(mask, frame.CapturedAt);
            tickStopwatch.Stop();
            _logger.LogTrace("Khung {Time:HH:mm:ss.fff}: lệch {Ratio:F2}, ô chữ {Cells}, {Change}",
                frame.CapturedAt, gate.LastChangeRatio, mask.TextCellCount, change);
            tickCount++;
            if (tickCount % TimingLogInterval == 0)
            {
                _logger.LogDebug("Vòng chụp: trung bình {Milliseconds:F1} ms/khung (chụp + cắt + mặt nạ, không tính OCR)",
                    tickStopwatch.Elapsed.TotalMilliseconds / TimingLogInterval);
                tickStopwatch.Reset();
            }

            if (change == SubtitleChange.Changed)
                recentCrops.Clear();

            if (recentCrops.Count < MaxBufferedFrames)
                recentCrops.Add(crop);

            if (change == SubtitleChange.Stable)
            {
                var (read, candidates) = await ReadSentenceAsync(recentCrops, area, frame.Width, frame.Height, gate.StillSince, cancellationToken);
                if (read is null)
                    continue;

                // Nền sau phụ đề chuyển động (nhân vật cử động, con trỏ chuột) làm vùng phụ đề trông như đổi dù câu vẫn vậy:
                // không báo câu mới, mà thêm phiếu cho câu đang hiện và chọn lại trên cả kho.
                if (SentenceComparer.AreSameSentence(read.Text, lastSentence))
                {
                    AddVotes(sentenceVotes, candidates);
                    string best = OcrVote.SelectMedian(sentenceVotes);
                    double gain = OcrVote.AverageDistance(lastSentence, sentenceVotes) - OcrVote.AverageDistance(best, sentenceVotes);
                    if (gain < MinCorrectionGain || CountCopies(sentenceVotes, best) < MinCorrectionCopies)
                    {
                        _logger.LogDebug("Trùng câu đang hiện, giữ nguyên ({Count} phiếu)", sentenceVotes.Count);
                        continue;
                    }

                    lastSentence = best;
                    _logger.LogInformation("Sửa phụ đề: \"{Text}\" ({Count} phiếu)", best, sentenceVotes.Count);
                    SentenceRead?.Invoke(read with { Text = best, IsCorrection = true });
                    continue;
                }

                sentenceVotes.Clear();
                AddVotes(sentenceVotes, candidates);
                lastSentence = read.Text;
                _logger.LogInformation("Đọc phụ đề: \"{Text}\" | OCR {OcrMs:F0} ms, trễ {LatencyMs:F0} ms",
                    read.Text, read.OcrDuration.TotalMilliseconds, read.Latency.TotalMilliseconds);
                SentenceRead?.Invoke(read);
            }
            else if (change == SubtitleChange.Cleared)
            {
                lastSentence = string.Empty;
                sentenceVotes.Clear();
                SubtitleCleared?.Invoke();
            }

        }
    }

    private static (Frame Crop, TextMask Mask) CropAndMask(Frame frame, PixelRect area)
    {
        var crop = FrameConverter.Crop(frame, area);
        return (crop, TextMask.FromFrame(crop));
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
