using System.Diagnostics;
using AppTranslator.Benchmark;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Evaluation;
using AppTranslator.Core.Pipeline;
using AppTranslator.Core.Profiles;
using AppTranslator.Imaging;
using AppTranslator.Ocr;
using AppTranslator.Storage;

// Benchmark OCR (Stage 3, Gate 1): chạy mọi engine × cách tiền xử lý trên bộ ảnh mẫu, so với ground-truth.csv.
// Cách dùng: AppTranslator.Benchmark [thư mục mẫu = samples/witcher3] [id hồ sơ = witcher3]
// Vùng chữ lấy từ hồ sơ game trong %AppData%\AppTranslator\profiles (giống app).

string samplesDirectory = args.Length > 0 ? args[0] : Path.Combine("samples", "witcher3");
string profileId = args.Length > 1 ? args[1] : "witcher3";

var profile = new JsonProfileStore(AppPaths.Default).Get(profileId)
    ?? throw new InvalidOperationException($"Không tìm thấy hồ sơ {profileId} trong {AppPaths.Default.ProfilesDirectory}");
var rows = GroundTruth.Load(Path.Combine(samplesDirectory, "ground-truth.csv"));
string outputDirectory = Path.Combine(samplesDirectory, "results", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
Directory.CreateDirectory(outputDirectory);

Console.WriteLine($"Hồ sơ: {profile.GameName}, {rows.Count} dòng ground truth, kết quả: {outputDirectory}");
foreach (var region in profile.Regions)
    Console.WriteLine($"  vùng {region.Type}: x={region.X} y={region.Y} w={region.W} h={region.H}");

var engines = CreateEngines();
var results = new List<BenchmarkResult>();
var images = new Dictionary<string, Frame>();

foreach (var mode in Enum.GetValues<PreprocessMode>())
{
    string cropDirectory = Path.Combine(outputDirectory, "crops", mode.ToString());
    Directory.CreateDirectory(cropDirectory);

    foreach (var row in rows)
    {
        var region = FindRegion(profile, row.Region);
        if (region is null)
        {
            Console.WriteLine($"Bỏ qua {row.ImageFile}: hồ sơ không có vùng {row.Region}");
            continue;
        }

        var crop = PrepareCrop(row, region, mode);
        FrameConverter.Save(crop, Path.Combine(cropDirectory, $"{Path.GetFileNameWithoutExtension(row.ImageFile)}-{row.Region}.png"));

        foreach (var engine in engines)
            results.Add(await RunAsync(engine, mode, row, crop));
    }
}

results.AddRange(BuildEnsembleResults(results));

BenchmarkReport.WriteCsv(results, Path.Combine(outputDirectory, "results.csv"));
BenchmarkReport.PrintSummary(results);

foreach (var engine in engines)
    (engine as IDisposable)?.Dispose();

return;

List<IOcrEngine> CreateEngines()
{
    var list = new List<IOcrEngine>();
    TryAdd(list, () => new WindowsOcrEngine());
    TryAdd(list, () => new RapidOcrEngine());
    return list;
}

void TryAdd(List<IOcrEngine> list, Func<IOcrEngine> create)
{
    try
    {
        var engine = create();
        list.Add(engine);
        Console.WriteLine($"Engine {engine.Name}: sẵn sàng");
    }
    catch (Exception exception)
    {
        // Thiếu gói ngôn ngữ OCR hoặc model: vẫn chạy các engine còn lại.
        Console.WriteLine($"Engine bị bỏ qua: {exception.Message}");
    }
}

Region? FindRegion(GameProfile gameProfile, RegionType type)
{
    foreach (var region in gameProfile.Regions)
    {
        if (region.Type == type)
            return region;
    }

    return null;
}

Frame PrepareCrop(GroundTruthRow row, Region region, PreprocessMode mode)
{
    if (!images.TryGetValue(row.ImageFile, out var image))
    {
        image = FrameConverter.Load(Path.Combine(samplesDirectory, "images", row.ImageFile));
        images[row.ImageFile] = image;
    }

    var crop = FrameConverter.Crop(image, region.ToPixelRect(image.Width, image.Height));
    return TextPreprocessor.Apply(crop, mode);
}

async Task<BenchmarkResult> RunAsync(IOcrEngine engine, PreprocessMode mode, GroundTruthRow row, Frame crop)
{
    // Lần chạy đầu của mỗi engine còn nạp model; chạy nháp một lần để số đo ms công bằng.
    if (results.Count < engines.Count)
        await engine.RecognizeAsync(crop);

    var stopwatch = Stopwatch.StartNew();
    var ocr = await engine.RecognizeAsync(crop);
    stopwatch.Stop();

    string recognized = ToText(ocr, row.Region, crop.Width);

    return new BenchmarkResult(
        engine.Name, mode, row, recognized,
        TextAccuracy.WordAccuracy(row.Text, recognized, ignoreCaseAndPunctuation: false),
        TextAccuracy.WordAccuracy(row.Text, recognized, ignoreCaseAndPunctuation: true),
        stopwatch.Elapsed.TotalMilliseconds);
}

// "Ensemble": với mỗi dòng, lấy kết quả ở giữa (OcrVote) của các cấu hình dưới đây; ms = tổng (chạy lần lượt).
// Tổ hợp chọn ở Stage 4 (bỏ Paddle + Upscale2x vì ~2 s/vùng).
List<BenchmarkResult> BuildEnsembleResults(List<BenchmarkResult> singleResults)
{
    var members = new HashSet<(string Engine, PreprocessMode Mode)>
    {
        ("WindowsOcr", PreprocessMode.None),
        ("WindowsOcr", PreprocessMode.Sharpen),
        ("WindowsOcr", PreprocessMode.Upscale2x),
        ("PaddleOcrV5", PreprocessMode.None),
        ("PaddleOcrV5", PreprocessMode.Sharpen),
    };

    var ensembleResults = new List<BenchmarkResult>();
    foreach (var row in rows)
    {
        var candidates = new List<string>();
        double totalMilliseconds = 0;
        foreach (var result in singleResults)
        {
            if (result.Row == row && members.Contains((result.Engine, result.Mode)))
            {
                candidates.Add(result.RecognizedText);
                totalMilliseconds += result.Milliseconds;
            }
        }

        if (candidates.Count == 0)
            continue;

        string chosen = OcrVote.SelectMedian(candidates);
        ensembleResults.Add(new BenchmarkResult(
            "Ensemble", PreprocessMode.None, row, chosen,
            TextAccuracy.WordAccuracy(row.Text, chosen, ignoreCaseAndPunctuation: false),
            TextAccuracy.WordAccuracy(row.Text, chosen, ignoreCaseAndPunctuation: true),
            totalMilliseconds));
    }

    return ensembleResults;
}

// Phụ đề đi qua SentenceAssembler giống app (ghép dòng, bỏ chữ HUD); lựa chọn hội thoại tách ở Stage 7 nên tạm nối thẳng.
string ToText(OcrResult ocr, RegionType region, int cropWidth)
{
    if (region == RegionType.Subtitle)
        return SentenceAssembler.Assemble(ocr.Lines, cropWidth);

    var lineTexts = new List<string>();
    foreach (var line in ocr.Lines)
        lineTexts.Add(line.Text);

    return string.Join(" ", lineTexts);
}
