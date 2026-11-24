using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AppTranslator.Core.Abstractions;
using AppTranslator.Storage;
using AppTranslator.Translation;

// Chạy thử Gemini thật bằng key đã lưu trong app (secrets.bin, DPAPI). Không in key ra.
// Cách dùng:
//   AppTranslator.TranslateCheck --models                       liệt kê model Flash key này dùng được
//   AppTranslator.TranslateCheck [file câu] [model] [id hồ sơ]   dịch lần lượt từng câu (kèm ngữ cảnh như app), đo độ trễ
// Mặc định: samples/witcher3/translate-check.txt, model trong settings.json, hồ sơ witcher3.

Console.OutputEncoding = Encoding.UTF8;
var paths = AppPaths.Default;
string? apiKey = new SecretStore(paths).Get(SecretStore.GeminiApiKey);
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("Chưa có key Gemini: mở app, chọn Bộ dịch = Gemini, nhập key rồi bấm Lưu key.");
    return 1;
}

using var httpClient = new HttpClient();
if (args.Length > 0 && args[0] == "--models")
{
    await ListModelsAsync();
    return 0;
}

string sentencesFile = args.Length > 0 ? args[0] : Path.Combine("samples", "witcher3", "translate-check.txt");
string model = args.Length > 1 ? args[1] : new JsonSettingsStore(paths).Load().GeminiModel;
string profileId = args.Length > 2 ? args[2] : "witcher3";
var profile = new JsonProfileStore(paths).Get(profileId)
    ?? throw new InvalidOperationException($"Không tìm thấy hồ sơ {profileId}");

var sentences = new List<string>();
foreach (string line in File.ReadAllLines(sentencesFile))
{
    if (line.Length > 0 && !line.StartsWith('#'))
        sentences.Add(line);
}

// Không dùng cache để lần nào cũng gọi Gemini thật.
var service = new TranslationService(new GeminiTranslator(httpClient, () => apiKey, model), cache: null);
Console.WriteLine($"Model {model}, hồ sơ {profile.GameName}, {sentences.Count} câu");
var durations = new List<double>();
foreach (string sentence in sentences)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        var result = await service.TranslateAsync(profile, sentence);
        stopwatch.Stop();
        if (result is null)
        {
            Console.WriteLine($"[bỏ qua: chữ rác] {sentence}");
            continue;
        }

        durations.Add(stopwatch.Elapsed.TotalMilliseconds);
        Console.WriteLine($"[{stopwatch.Elapsed.TotalMilliseconds,5:F0} ms] EN: {sentence}");
        Console.WriteLine($"           VI: {result.TranslatedText}");
    }
    catch (TranslationException exception)
    {
        Console.WriteLine($"[lỗi {exception.Kind}] {exception.Message} | {sentence}");
        if (exception.Kind is TranslationErrorKind.InvalidKey or TranslationErrorKind.QuotaExceeded)
            break;
    }
}

if (durations.Count > 0)
{
    durations.Sort();
    Console.WriteLine($"Độ trễ dịch: trung bình {durations.Average():F0} ms, trung vị {durations[durations.Count / 2]:F0} ms, " +
        $"lớn nhất {durations[^1]:F0} ms ({durations.Count} câu)");
}

return 0;

async Task ListModelsAsync()
{
    using var request = new HttpRequestMessage(HttpMethod.Get, "https://generativelanguage.googleapis.com/v1beta/models?pageSize=1000");
    request.Headers.Add("x-goog-api-key", apiKey);
    using var response = await httpClient.SendAsync(request);
    string body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"Lỗi {(int)response.StatusCode} khi liệt kê model");
        return;
    }

    using var document = JsonDocument.Parse(body);
    foreach (var item in document.RootElement.GetProperty("models").EnumerateArray())
    {
        string name = item.GetProperty("name").GetString() ?? string.Empty;
        bool canGenerate = item.TryGetProperty("supportedGenerationMethods", out var methods)
            && methods.EnumerateArray().Any(method => method.GetString() == "generateContent");
        if (canGenerate && name.Contains("flash", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine(name.Replace("models/", string.Empty));
    }
}
