using AppTranslator.Core.Abstractions;

namespace AppTranslator.Translation;

/// <summary>Bộ dịch giả khi chưa có key: trả lại câu gốc có đánh dấu, để chạy thử trọn luồng đọc → dịch → hiện.</summary>
public sealed class MockTranslator : ITranslator
{
    public const string ProviderName = "Mock";

    public Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TranslationResult($"[Dịch thử] {request.SourceText}", ProviderName, "mock"));
    }
}
