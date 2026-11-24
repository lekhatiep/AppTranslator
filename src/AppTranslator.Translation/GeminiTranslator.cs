using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppTranslator.Core.Abstractions;

namespace AppTranslator.Translation;

/// <summary>
/// Dịch bằng Gemini API (REST generateContent). Key lấy qua hàm <c>getApiKey</c> mỗi lần gọi, để người dùng đổi key
/// không cần mở lại app. Key gửi qua header, không nằm trong URL nên không lọt vào log.
/// </summary>
public sealed class GeminiTranslator : ITranslator
{
    public const string ProviderName = "Gemini";

    private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models/";

    // Ngân sách độ trễ cả luồng là 1.5 s; quá ngần này thì báo lỗi để người chơi biết, không chờ mãi.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    // Thấp để bản dịch ổn định (cùng câu dịch giống nhau), vẫn đủ tự nhiên.
    private const double Temperature = 0.3;

    private readonly HttpClient _httpClient;
    private readonly Func<string?> _getApiKey;
    private readonly string _model;

    public GeminiTranslator(HttpClient httpClient, Func<string?> getApiKey, string model)
    {
        _httpClient = httpClient;
        _getApiKey = getApiKey;
        _model = model;
    }

    public async Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        string? apiKey = _getApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new TranslationException(TranslationErrorKind.InvalidKey, "Chưa nhập key Gemini");

        var body = new GenerateContentRequest(
            new Content(null, [new Part(PromptBuilder.BuildInstruction(request.Profile))]),
            [new Content("user", [new Part(PromptBuilder.BuildUserMessage(request))])],
            new GenerationConfig(Temperature));

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}{_model}:generateContent")
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        message.Headers.Add("x-goog-api-key", apiKey);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(message, timeout.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TranslationException(TranslationErrorKind.Timeout, "Gemini phản hồi quá chậm", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new TranslationException(TranslationErrorKind.Network, "Không kết nối được Gemini (mất mạng?)", exception);
        }

        using (response)
        {
            string responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw ToException(response.StatusCode, responseText);

            return new TranslationResult(ReadTranslation(responseText), ProviderName, _model);
        }
    }

    private static TranslationException ToException(HttpStatusCode status, string responseText)
    {
        // Key sai: Gemini trả 400 kèm lý do API_KEY_INVALID; key bị chặn/thiếu quyền: 401/403.
        bool invalidKey = status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || (status == HttpStatusCode.BadRequest && responseText.Contains("API_KEY_INVALID", StringComparison.Ordinal));
        if (invalidKey)
            return new TranslationException(TranslationErrorKind.InvalidKey, "Key Gemini sai hoặc không có quyền");

        if (status == HttpStatusCode.TooManyRequests)
            return new TranslationException(TranslationErrorKind.QuotaExceeded, "Hết lượt dịch Gemini, thử lại sau");

        if ((int)status >= 500)
            return new TranslationException(TranslationErrorKind.ServiceUnavailable, $"Gemini đang lỗi ({(int)status})");

        return new TranslationException(TranslationErrorKind.Other, $"Gemini trả lỗi {(int)status}");
    }

    private static string ReadTranslation(string responseText)
    {
        GenerateContentResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<GenerateContentResponse>(responseText, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new TranslationException(TranslationErrorKind.Other, "Không đọc được phản hồi Gemini", exception);
        }

        var parts = response?.Candidates?.FirstOrDefault()?.Content?.Parts;
        if (parts is null || parts.Count == 0)
        {
            // Bị chặn vì nội dung (safety) hoặc không sinh ra gì.
            throw new TranslationException(TranslationErrorKind.Other, "Gemini không trả bản dịch cho câu này");
        }

        var texts = new List<string>();
        foreach (var part in parts)
        {
            if (!string.IsNullOrEmpty(part.Text))
                texts.Add(part.Text);
        }

        string translation = string.Concat(texts).Trim().Trim('"', '“', '”').Trim();
        if (translation.Length == 0)
            throw new TranslationException(TranslationErrorKind.Other, "Gemini trả bản dịch rỗng");

        return translation;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record GenerateContentRequest(Content SystemInstruction, IReadOnlyList<Content> Contents, GenerationConfig GenerationConfig);

    private sealed record Content(string? Role, IReadOnlyList<Part>? Parts);

    private sealed record Part(string? Text);

    private sealed record GenerationConfig(double Temperature);

    private sealed record GenerateContentResponse(IReadOnlyList<Candidate>? Candidates);

    private sealed record Candidate(Content? Content);
}
