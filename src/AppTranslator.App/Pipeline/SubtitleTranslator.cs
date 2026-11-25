using System.Net.Http;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Profiles;
using AppTranslator.Core.Text;
using AppTranslator.Storage;
using AppTranslator.Translation;
using Microsoft.Extensions.Logging;

namespace AppTranslator.App.Pipeline;

/// <param name="Translation">null khi câu trông như chữ rác (không gửi dịch) hoặc khi lỗi.</param>
/// <param name="Error">Lỗi dịch (key, quota, mạng...) để báo cho người chơi.</param>
public sealed record SubtitleTranslation(string Source, string? Translation, bool FromCache, TimeSpan TranslateDuration, TranslationException? Error);

/// <param name="Lines">Theo đúng thứ tự lựa chọn; phần tử null khi lựa chọn đó trông như chữ rác (không dịch).</param>
public sealed record ChoicesTranslation(IReadOnlyList<TranslatedLine?> Lines, TranslationException? Error);

/// <summary>
/// Nối vòng đọc phụ đề với bước dịch: chọn bộ dịch theo cài đặt, bỏ qua lần sửa câu nhỏ, và chỉ trả kết quả của câu
/// mới nhất (câu cũ dịch xong muộn không được đè lên câu mới). Gọi trên luồng UI.
/// </summary>
public sealed class SubtitleTranslator
{
    public static readonly IReadOnlyList<string> Providers = [MockTranslator.ProviderName, GeminiTranslator.ProviderName];

    // Câu được sửa lại khác câu đã dịch ít hơn ngần này từ thì không dịch lại: AI đã tự hiểu lỗi nhỏ ("Butyou" / "But you"),
    // dịch lại chỉ tốn lượt gọi mà bản dịch gần như không đổi.
    private const int MinChangedWordsToRetranslate = 3;

    private readonly SecretStore _secrets;
    private readonly SqliteTranslationCache _cache;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SubtitleTranslator> _logger;
    private TranslationService _service;
    // Lựa chọn dùng bộ dịch riêng để không chen vào ngữ cảnh câu thoại của phụ đề; dùng chung cache.
    private TranslationService _choiceService;
    // Vùng khoanh thủ công (FR-07): bộ dịch riêng, không chen vào ngữ cảnh hội thoại.
    private TranslationService _areaService;
    private int _sequence;
    private int _choiceSequence;
    private string _lastTranslatedSource = string.Empty;

    public SubtitleTranslator(SecretStore secrets, SqliteTranslationCache cache, HttpClient httpClient, ILogger<SubtitleTranslator> logger)
    {
        _secrets = secrets;
        _cache = cache;
        _httpClient = httpClient;
        _logger = logger;
        _service = new TranslationService(new MockTranslator(), cache: null);
        _choiceService = new TranslationService(new MockTranslator(), cache: null);
        _areaService = new TranslationService(new MockTranslator(), cache: null);
    }

    public void UseProvider(string provider, string geminiModel)
    {
        if (provider == GeminiTranslator.ProviderName)
        {
            var gemini = new GeminiTranslator(_httpClient, () => _secrets.Get(SecretStore.GeminiApiKey), geminiModel);
            _service = new TranslationService(gemini, _cache);
            _choiceService = new TranslationService(gemini, _cache);
            _areaService = new TranslationService(gemini, _cache);
        }
        else
        {
            // Bản dịch giả không ghi vào cache, để không lẫn với bản dịch thật.
            _service = new TranslationService(new MockTranslator(), cache: null);
            _choiceService = new TranslationService(new MockTranslator(), cache: null);
            _areaService = new TranslationService(new MockTranslator(), cache: null);
        }

        Reset();
        ResetChoices();
        _logger.LogInformation("Bộ dịch: {Provider} ({Model})", provider, provider == GeminiTranslator.ProviderName ? geminiModel : "-");
    }

    /// <summary>Phụ đề tắt hoặc đổi nguồn: bỏ kết quả của câu đang dịch dở.</summary>
    public void Reset()
    {
        _sequence++;
        _lastTranslatedSource = string.Empty;
    }

    /// <summary>Trả về null nếu bỏ qua (lần sửa nhỏ, hoặc đã có câu mới hơn trong lúc dịch).</summary>
    public async Task<SubtitleTranslation?> TranslateAsync(GameProfile profile, SubtitleRead read, CancellationToken cancellationToken = default)
    {
        if (read.IsCorrection && CountChangedWords(_lastTranslatedSource, read.Text) < MinChangedWordsToRetranslate)
        {
            _logger.LogDebug("Không dịch lại câu sửa nhỏ: \"{Text}\"", read.Text);
            return null;
        }

        int sequence = ++_sequence;
        try
        {
            var line = await _service.TranslateAsync(profile, read.Text, cancellationToken);
            if (sequence != _sequence)
                return null;

            if (line is null)
            {
                _logger.LogInformation("Không dịch câu trông như chữ rác: \"{Text}\"", read.Text);
                return new SubtitleTranslation(read.Text, null, false, TimeSpan.Zero, null);
            }

            _lastTranslatedSource = read.Text;
            _logger.LogInformation("Dịch: \"{Translation}\" | {Source}, {Milliseconds:F0} ms, tổng từ lúc câu hiện {TotalMs:F0} ms",
                line.TranslatedText, line.FromCache ? "cache" : "AI", line.Elapsed.TotalMilliseconds,
                (read.Latency + line.Elapsed).TotalMilliseconds);
            return new SubtitleTranslation(read.Text, line.TranslatedText, line.FromCache, line.Elapsed, null);
        }
        catch (TranslationException exception)
        {
            _logger.LogWarning("Dịch lỗi ({Kind}): {Message}", exception.Kind, exception.Message);
            if (sequence != _sequence)
                return null;

            return new SubtitleTranslation(read.Text, null, false, TimeSpan.Zero, exception);
        }
    }

    /// <summary>Bảng lựa chọn tắt hoặc đổi nguồn: bỏ kết quả của bộ lựa chọn đang dịch dở.</summary>
    public void ResetChoices()
    {
        _choiceSequence++;
    }

    /// <summary>
    /// Dịch từng lựa chọn (cache theo từng câu, nên bảng lựa chọn lặp lại hiện ngay). Gọi song song để cả bảng chỉ chờ bằng
    /// câu chậm nhất. Trả về null nếu đã có bảng lựa chọn mới hơn trong lúc dịch.
    /// </summary>
    public async Task<ChoicesTranslation?> TranslateChoicesAsync(GameProfile profile, IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        int sequence = ++_choiceSequence;
        var tasks = new List<Task<TranslatedLine?>>();
        foreach (string text in texts)
            tasks.Add(_choiceService.TranslateAsync(profile, text, cancellationToken));

        try
        {
            var lines = await Task.WhenAll(tasks);
            if (sequence != _choiceSequence)
                return null;

            _logger.LogInformation("Dịch lựa chọn: {Translations}", string.Join(" | ", lines.Select(line => line?.TranslatedText ?? "(không dịch)")));
            return new ChoicesTranslation(lines, null);
        }
        catch (TranslationException exception)
        {
            _logger.LogWarning("Dịch lựa chọn lỗi ({Kind}): {Message}", exception.Kind, exception.Message);
            if (sequence != _choiceSequence)
                return null;

            return new ChoicesTranslation([], exception);
        }
    }

    /// <summary>
    /// Dịch chữ trong vùng khoanh thủ công. Trả về null nếu chữ trông như rác (không gửi dịch).
    /// Ném <see cref="TranslationException"/> khi lỗi key, quota, mạng...
    /// </summary>
    public Task<TranslatedLine?> TranslateAreaAsync(GameProfile profile, string text, CancellationToken cancellationToken = default)
    {
        return _areaService.TranslateAsync(profile, text, cancellationToken);
    }

    public Task ClearCacheAsync(string profileId)
    {
        return _cache.ClearAsync(profileId);
    }

    private static int CountChangedWords(string first, string second)
    {
        return WordDistance.Between(ToWords(first), ToWords(second));
    }

    private static string[] ToWords(string text)
    {
        var words = new List<string>();
        foreach (string word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            string normalized = TextNormalizer.ForComparison(word);
            if (normalized.Length > 0)
                words.Add(normalized);
        }

        return words.ToArray();
    }
}
