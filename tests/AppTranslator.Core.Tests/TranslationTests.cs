using System.Net;
using System.Text;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Profiles;
using AppTranslator.Translation;

namespace AppTranslator.Core.Tests;

public class PromptBuilderTests
{
    [Fact]
    public void Instruction_IncludesProfileStyleNamesAndGlossary()
    {
        string instruction = PromptBuilder.BuildInstruction(TestProfiles.Witcher3());

        Assert.Contains("The Witcher 3: Wild Hunt", instruction);
        Assert.Contains("OCR", instruction);
        Assert.Contains("Trung cổ kỳ ảo", instruction);
        Assert.Contains("Geralt xưng 'ta'", instruction);
        Assert.Contains("Geralt, Kaer Morhen", instruction);
        Assert.Contains("the Wild Hunt => Đoàn Săn Hoang Dã", instruction);
    }

    [Fact]
    public void UserMessage_IncludesPreviousLinesThenCurrentLine()
    {
        var request = new TranslationRequest(
            "But you were asleep, Uncle Vesemir.",
            TestProfiles.Witcher3(),
            [new ContextLine("Huh.", "Hừm.")]);

        string message = PromptBuilder.BuildUserMessage(request);

        Assert.Contains("EN: Huh.\nVI: Hừm.", message.Replace("\r\n", "\n"));
        Assert.EndsWith("Current line:\nBut you were asleep, Uncle Vesemir.", message.Replace("\r\n", "\n"));
    }

    [Fact]
    public void UserMessage_NoContext_OnlyCurrentLine()
    {
        string message = PromptBuilder.BuildUserMessage(new TranslationRequest("Hm? What?", TestProfiles.Witcher3(), []));

        Assert.DoesNotContain("Previous", message);
        Assert.EndsWith("Hm? What?", message);
    }
}

public class GeminiTranslatorTests
{
    private const string SuccessBody = """
        {"candidates":[{"content":{"role":"model","parts":[{"text":"\"Nhưng chú đang ngủ mà, chú Vesemir.\"\n"}]}}]}
        """;

    [Fact]
    public async Task Success_ReturnsTrimmedTranslation_AndSendsKeyInHeader()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, SuccessBody);
        var translator = new GeminiTranslator(new HttpClient(handler), () => "test-key", "gemini-test");

        var result = await translator.TranslateAsync(Request());

        Assert.Equal("Nhưng chú đang ngủ mà, chú Vesemir.", result.TranslatedText);
        Assert.Equal("Gemini", result.Provider);
        Assert.Equal("gemini-test", result.Model);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-test:generateContent", handler.RequestUri);
        Assert.Equal("test-key", handler.ApiKeyHeader);
        Assert.DoesNotContain("test-key", handler.RequestUri);
        Assert.Contains("\"systemInstruction\"", handler.RequestBody);
        Assert.Contains("But you were asleep", handler.RequestBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"status":"INVALID_ARGUMENT","details":[{"reason":"API_KEY_INVALID"}]}}""", TranslationErrorKind.InvalidKey)]
    [InlineData(HttpStatusCode.Forbidden, "{}", TranslationErrorKind.InvalidKey)]
    [InlineData(HttpStatusCode.TooManyRequests, "{}", TranslationErrorKind.QuotaExceeded)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}", TranslationErrorKind.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest, "{}", TranslationErrorKind.Other)]
    [InlineData(HttpStatusCode.OK, """{"candidates":[]}""", TranslationErrorKind.Other)]
    [InlineData(HttpStatusCode.OK, "not json", TranslationErrorKind.Other)]
    public async Task Errors_MappedToKind(HttpStatusCode status, string body, TranslationErrorKind expected)
    {
        var translator = new GeminiTranslator(new HttpClient(new FakeHandler(status, body)), () => "test-key", "gemini-test");

        var exception = await Assert.ThrowsAsync<TranslationException>(() => translator.TranslateAsync(Request()));

        Assert.Equal(expected, exception.Kind);
    }

    [Fact]
    public async Task NoKey_InvalidKeyWithoutCallingApi()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, SuccessBody);
        var translator = new GeminiTranslator(new HttpClient(handler), () => null, "gemini-test");

        var exception = await Assert.ThrowsAsync<TranslationException>(() => translator.TranslateAsync(Request()));

        Assert.Equal(TranslationErrorKind.InvalidKey, exception.Kind);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task NetworkFailure_Network()
    {
        var translator = new GeminiTranslator(new HttpClient(new FakeHandler(new HttpRequestException("no route"))), () => "k", "m");

        var exception = await Assert.ThrowsAsync<TranslationException>(() => translator.TranslateAsync(Request()));

        Assert.Equal(TranslationErrorKind.Network, exception.Kind);
    }

    private static TranslationRequest Request()
    {
        return new TranslationRequest("But you were asleep, Uncle Vesemir.", TestProfiles.Witcher3(), []);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body = string.Empty;
        private readonly Exception? _failure;

        public FakeHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public FakeHandler(Exception failure)
        {
            _failure = failure;
        }

        public string? RequestUri { get; private set; }
        public string? ApiKeyHeader { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_failure is not null)
                throw _failure;

            RequestUri = request.RequestUri!.ToString();
            ApiKeyHeader = request.Headers.GetValues("x-goog-api-key").Single();
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }
}

public class TranslationServiceTests
{
    [Fact]
    public async Task NewLine_CallsTranslator_ThenCacheHitDoesNot()
    {
        var translator = new CountingTranslator();
        var cache = new MemoryCache();
        var profile = TestProfiles.Witcher3();
        var service = new TranslationService(translator, cache);

        var first = await service.TranslateAsync(profile, "Hm? What?");
        // Câu khác xen giữa để lần sau không phải "trùng câu ngay trước".
        await service.TranslateAsync(profile, "End of discussion.");
        var again = await service.TranslateAsync(profile, "Hm?What?");

        Assert.False(first!.FromCache);
        Assert.True(again!.FromCache);
        Assert.Equal("VI(Hm? What?)", again.TranslatedText);
        Assert.Equal(2, translator.Calls);
    }

    [Fact]
    public async Task SameAsPreviousLine_NoTranslatorCall_EvenWithoutCache()
    {
        var translator = new CountingTranslator();
        var service = new TranslationService(translator, cache: null);
        var profile = TestProfiles.Witcher3();

        await service.TranslateAsync(profile, "But you were asleep, Uncle Vesemir.");
        var repeat = await service.TranslateAsync(profile, "Butyou were asleep, Uncle Vesemir");

        Assert.True(repeat!.FromCache);
        Assert.Equal(1, translator.Calls);
    }

    [Fact]
    public async Task Garbage_Skipped()
    {
        var translator = new CountingTranslator();
        var service = new TranslationService(translator, cache: null);

        var result = await service.TranslateAsync(TestProfiles.Witcher3(), "'Viljåble. Sh&uldjl turn backan 11");

        Assert.Null(result);
        Assert.Equal(0, translator.Calls);
    }

    [Fact]
    public async Task Context_KeepsLastLinesOfProfile()
    {
        var translator = new CountingTranslator();
        var profile = TestProfiles.Witcher3() with { Style = new StyleOptions { ContextLines = 2 } };
        var service = new TranslationService(translator, cache: null);

        await service.TranslateAsync(profile, "One line.");
        await service.TranslateAsync(profile, "Two lines.");
        await service.TranslateAsync(profile, "Three lines.");
        await service.TranslateAsync(profile, "Four lines.");

        var context = translator.LastRequest!.PreviousLines;
        Assert.Equal(["Two lines.", "Three lines."], context.Select(line => line.Source));
        Assert.Equal("VI(Three lines.)", context[1].Translation);
    }

    [Fact]
    public async Task ProfileChange_ResetsContext()
    {
        var translator = new CountingTranslator();
        var service = new TranslationService(translator, cache: null);

        await service.TranslateAsync(TestProfiles.Witcher3(), "One line.");
        await service.TranslateAsync(TestProfiles.Witcher3() with { Id = "other" }, "Two lines.");

        Assert.Empty(translator.LastRequest!.PreviousLines);
    }

    [Fact]
    public async Task TranslatorError_Propagates_AndNothingCached()
    {
        var cache = new MemoryCache();
        var service = new TranslationService(new FailingTranslator(), cache);

        await Assert.ThrowsAsync<TranslationException>(() => service.TranslateAsync(TestProfiles.Witcher3(), "Hm? What?"));
        Assert.Empty(cache.Entries);
    }

    private sealed class CountingTranslator : ITranslator
    {
        public int Calls { get; private set; }
        public TranslationRequest? LastRequest { get; private set; }

        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(new TranslationResult($"VI({request.SourceText})", "Test", "test"));
        }
    }

    private sealed class FailingTranslator : ITranslator
    {
        public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
        {
            throw new TranslationException(TranslationErrorKind.QuotaExceeded, "Hết lượt");
        }
    }

    /// <summary>Cache trong bộ nhớ, khóa theo câu đã chuẩn hóa giống bản SQLite.</summary>
    private sealed class MemoryCache : ITranslationCache
    {
        public Dictionary<string, TranslationResult> Entries { get; } = [];

        public Task<TranslationResult?> GetAsync(GameProfile profile, string sourceText, CancellationToken cancellationToken = default)
        {
            Entries.TryGetValue(Key(profile, sourceText), out var result);
            return Task.FromResult(result);
        }

        public Task SetAsync(GameProfile profile, string sourceText, TranslationResult result, CancellationToken cancellationToken = default)
        {
            Entries[Key(profile, sourceText)] = result;
            return Task.CompletedTask;
        }

        public Task ClearAsync(string profileId, CancellationToken cancellationToken = default)
        {
            Entries.Clear();
            return Task.CompletedTask;
        }

        private static string Key(GameProfile profile, string sourceText)
        {
            return $"{profile.Id}|{profile.Version}|{Text.TextNormalizer.ForComparison(sourceText)}";
        }
    }
}

internal static class TestProfiles
{
    public static GameProfile Witcher3()
    {
        return new GameProfile
        {
            Id = "witcher3",
            GameName = "The Witcher 3: Wild Hunt",
            Style = new StyleOptions { Tone = "Trung cổ kỳ ảo, lời thoại tự nhiên", Addressing = "Geralt xưng 'ta', gọi người lạ 'ngươi'" },
            KeepNames = ["Geralt", "Kaer Morhen"],
            Glossary = [new GlossaryEntry("the Wild Hunt", "Đoàn Săn Hoang Dã")],
        };
    }
}
