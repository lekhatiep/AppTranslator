using System.Diagnostics;
using System.IO;
using AppTranslator.App.Pipeline;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Profiles;
using AppTranslator.Imaging;
using AppTranslator.Storage;
using AppTranslator.VideoReplay;
using OpenCvSharp;

// Phát lại video quay màn hình qua đúng SubtitleReader của app, theo thời gian thật (10 khung/giây như khi chơi),
// để debug đọc sai/đọc lặp. Ghi log từng khung (độ lệch mặt nạ, sự kiện) và lưu ảnh cắt mỗi lần đọc câu.
// Cách dùng: AppTranslator.VideoReplay <file video> [id hồ sơ = witcher3]
// Vùng chữ lấy từ hồ sơ trong %AppData%\AppTranslator\profiles (giống app). Video nên có cùng tỉ lệ với cửa sổ game.

if (args.Length == 0)
{
    Console.WriteLine("Cách dùng: AppTranslator.VideoReplay <file video> [id hồ sơ = witcher3]");
    return 1;
}

string videoPath = args[0];
string profileId = args.Length > 1 ? args[1] : "witcher3";
var profile = new JsonProfileStore(AppPaths.Default).Get(profileId)
    ?? throw new InvalidOperationException($"Không tìm thấy hồ sơ {profileId}");
var region = profile.Regions.First(r => r.Type == RegionType.Subtitle);

using var video = new VideoCapture(videoPath);
if (!video.IsOpened())
    throw new FileNotFoundException("Không mở được video", videoPath);

double framesPerSecond = video.Fps;
string outputDirectory = Path.Combine(
    Path.GetDirectoryName(Path.GetFullPath(videoPath))!, "replay", $"{Path.GetFileNameWithoutExtension(videoPath)}-{DateTime.Now:yyyyMMdd-HHmmss}");
Directory.CreateDirectory(outputDirectory);
using var logger = new FileLogger(Path.Combine(outputDirectory, "replay.log"));
Console.WriteLine($"Video {video.FrameWidth}x{video.FrameHeight}, {framesPerSecond:F1} fps, {video.FrameCount} khung. Kết quả: {outputDirectory}");

var startedAt = DateTimeOffset.Now;
var playback = Stopwatch.StartNew();
using var currentMat = new Mat();
int currentIndex = -1;
Frame? lastFrame = null;
int sentenceCount = 0;

var reader = new SubtitleReader(logger);
reader.SentenceRead += read =>
{
    sentenceCount++;
    Console.WriteLine($"[{VideoTime(currentIndex)}] câu {sentenceCount}: {read.Text}");
    if (lastFrame is not null)
    {
        var crop = FrameConverter.Crop(lastFrame, region.ToPixelRect(lastFrame.Width, lastFrame.Height));
        FrameConverter.Save(crop, Path.Combine(outputDirectory, $"cau-{sentenceCount:D3}-{VideoTime(currentIndex).Replace(':', '.')}.png"));
    }
};
reader.SubtitleCleared += () => Console.WriteLine($"[{VideoTime(currentIndex)}] phụ đề tắt");

using var cancellation = new CancellationTokenSource();
try
{
    await reader.RunAsync(_ => Task.FromResult(NextFrame()), region, cancellation.Token);
}
catch (OperationCanceledException)
{
    // Hết video.
}

Console.WriteLine($"Xong: {sentenceCount} lần đọc câu. Log từng khung: {Path.Combine(outputDirectory, "replay.log")}");
return 0;

// Lấy khung của video tại thời điểm đang phát (bỏ qua các khung ở giữa, giống chụp màn hình thật).
Frame? NextFrame()
{
    int wantedIndex = (int)(playback.Elapsed.TotalSeconds * framesPerSecond);
    while (currentIndex < wantedIndex)
    {
        if (!video.Read(currentMat) || currentMat.Empty())
        {
            cancellation.Cancel();
            return null;
        }

        currentIndex++;
    }

    var videoTime = TimeSpan.FromSeconds(currentIndex / framesPerSecond);
    lastFrame = FrameConverter.ToFrame(currentMat) with { CapturedAt = startedAt + videoTime };
    return lastFrame;
}

string VideoTime(int frameIndex)
{
    return TimeSpan.FromSeconds(Math.Max(0, frameIndex) / framesPerSecond).ToString(@"mm\:ss\.f");
}
