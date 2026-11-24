using System.Diagnostics;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Pipeline;
using AppTranslator.Core.Profiles;
using AppTranslator.Imaging;
using AppTranslator.Ocr;
using Microsoft.Extensions.Logging;

namespace AppTranslator.App.Pipeline;

/// <param name="Text">Câu tiếng Anh đã ghép.</param>
/// <param name="OcrDuration">Thời gian tiền xử lý + OCR các khung bỏ phiếu.</param>
/// <param name="Latency">Từ khung đầu tiên thấy câu này tới khi đọc xong (gồm cả thời gian chờ chữ đứng yên).</param>
public sealed record SubtitleRead(string Text, TimeSpan OcrDuration, TimeSpan Latency);

/// <summary>
/// Vòng đọc phụ đề (FR-04): chụp ~10 khung/giây → cắt vùng phụ đề → chờ chữ đứng yên → OCR vài khung → chọn kết quả ở giữa.
/// Gọi chụp trên luồng đã gọi RunAsync (luồng UI, giống các nút chụp khác) để hai engine chụp không bị gọi từ hai luồng;
/// phần tốn CPU (cắt ảnh, mặt nạ, làm nét) chạy nền. Sự kiện được gọi trên luồng đã gọi RunAsync.
/// </summary>
public sealed class SubtitleReader
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan StableDuration = TimeSpan.FromMilliseconds(300);

    // Số khung gần nhất đem OCR rồi bỏ phiếu: lỗi ngẫu nhiên của một khung (nhiễu nén) bị loại.
    private const int VoteFrameCount = 3;
    private const int MaxBufferedFrames = 10;

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

            if (change == SubtitleChange.Changed)
                recentCrops.Clear();

            if (recentCrops.Count < MaxBufferedFrames)
                recentCrops.Add(crop);

            if (change == SubtitleChange.Stable)
                await ReadSentenceAsync(recentCrops, gate.StillSince, cancellationToken);
            else if (change == SubtitleChange.Cleared)
                SubtitleCleared?.Invoke();

            tickCount++;
            if (tickCount % TimingLogInterval == 0)
            {
                _logger.LogDebug("Vòng chụp: trung bình {Milliseconds:F1} ms/khung (chụp + cắt + mặt nạ, không tính OCR)",
                    tickStopwatch.Elapsed.TotalMilliseconds / TimingLogInterval);
                tickStopwatch.Reset();
            }
        }
    }

    private static (Frame Crop, TextMask Mask) CropAndMask(Frame frame, PixelRect area)
    {
        var crop = FrameConverter.Crop(frame, area);
        return (crop, TextMask.FromFrame(crop));
    }

    private async Task ReadSentenceAsync(List<Frame> recentCrops, DateTimeOffset firstSeenAt, CancellationToken cancellationToken)
    {
        // Lấy các khung cuối: khung đầu của lần đứng yên có thể còn đang chuyển cảnh/hiện dần.
        int start = Math.Max(0, recentCrops.Count - VoteFrameCount);
        var stopwatch = Stopwatch.StartNew();
        var candidates = new List<string>();
        for (int i = start; i < recentCrops.Count; i++)
        {
            var crop = recentCrops[i];
            var sharpened = await Task.Run(() => TextPreprocessor.Apply(crop, PreprocessMode.Sharpen), cancellationToken);
            var result = await _ocr!.RecognizeAsync(sharpened, cancellationToken);
            candidates.Add(SentenceAssembler.Assemble(result.Lines, crop.Width));
        }

        string text = OcrVote.SelectMedian(candidates);
        stopwatch.Stop();
        cancellationToken.ThrowIfCancellationRequested();

        var latency = DateTimeOffset.Now - firstSeenAt;
        if (text.Length == 0)
        {
            // Nền sáng đứng yên nhưng không có chữ đọc được.
            _logger.LogDebug("Vùng phụ đề đứng yên nhưng OCR không ra chữ ({Count} khung)", candidates.Count);
            return;
        }

        _logger.LogInformation("Đọc phụ đề: \"{Text}\" | OCR {Count} khung {OcrMs:F0} ms, trễ {LatencyMs:F0} ms",
            text, candidates.Count, stopwatch.Elapsed.TotalMilliseconds, latency.TotalMilliseconds);
        SentenceRead?.Invoke(new SubtitleRead(text, stopwatch.Elapsed, latency));
    }
}
