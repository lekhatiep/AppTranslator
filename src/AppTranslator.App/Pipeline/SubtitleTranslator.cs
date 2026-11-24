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
    private int _sequence;
    private string _lastTranslatedSource = string.Empty;

    public SubtitleTranslator(SecretStore secrets, SqliteTranslationCache cache, HttpClient httpClient, ILogger<SubtitleTranslator> logger)
    {
        _secrets = secrets;
        _cache = cache;
        _httpClient = httpClient;
        _logger = logger;
        _service = new TranslationService(new MockTranslator(), cache: null);
    }

    public void UseProvider(string provider, string geminiModel)
    {
        if (provider == GeminiTranslator.ProviderName)
        {
            var gemini = new GeminiTranslator(_httpClient, () => _secrets.Get(SecretStore.GeminiApiKey), geminiModel);
            _service = new TranslationService(gemini, _cache);
        }
        else
        {
            // Bản dịch giả không ghi vào cache, để không lẫn với bản dịch thật.
            _service = new TranslationService(new MockTranslator(), cache: null);
        }

        Reset();
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
