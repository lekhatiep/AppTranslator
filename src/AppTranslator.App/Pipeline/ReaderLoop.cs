using System.Diagnostics;
using AppTranslator.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace AppTranslator.App.Pipeline;

/// <summary>Một bước xử lý nhận từng khung hình chụp được (đọc phụ đề, đọc lựa chọn...).</summary>
public interface IFrameProcessor
{
    Task ProcessAsync(Frame frame, CancellationToken cancellationToken);
}

/// <summary>
/// Vòng chụp chung ~10 khung/giây: mỗi khung chỉ chụp một lần rồi đưa cho mọi bộ đọc (chụp cả cửa sổ tốn ~15 ms).
/// Gọi chụp trên luồng đã gọi RunAsync (luồng UI, giống các nút chụp khác) để hai engine chụp không bị gọi từ hai luồng.
/// Bộ đọc đang OCR thì khung sau chờ; PeriodicTimer tự bỏ các nhịp bị lỡ.
/// </summary>
public static class ReaderLoop
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(100);

    // Cứ ngần này khung thì ghi log thời gian chụp trung bình (đo tải của vòng chụp).
    private const int TimingLogInterval = 100;

    /// <summary>Chạy tới khi bị hủy (ném OperationCanceledException).</summary>
    public static async Task RunAsync(
        Func<CancellationToken, Task<Frame?>> captureWindow,
        IReadOnlyList<IFrameProcessor> processors,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var captureStopwatch = new Stopwatch();
        int frameCount = 0;
        using var timer = new PeriodicTimer(FrameInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            captureStopwatch.Start();
            var frame = await captureWindow(cancellationToken);
            captureStopwatch.Stop();
            if (frame is null)
                continue;

            foreach (var processor in processors)
                await processor.ProcessAsync(frame, cancellationToken);

            frameCount++;
            if (frameCount % TimingLogInterval == 0)
            {
                logger.LogDebug("Vòng chụp: chụp trung bình {Milliseconds:F1} ms/khung", captureStopwatch.Elapsed.TotalMilliseconds / TimingLogInterval);
                captureStopwatch.Reset();
            }
        }
    }
}
