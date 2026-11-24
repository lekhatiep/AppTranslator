using AppTranslator.Core.Profiles;

namespace AppTranslator.Core.Abstractions;

/// <summary>Một câu đã dịch trước đó, gửi kèm làm ngữ cảnh (FR-09) để giữ xưng hô và văn phong.</summary>
public sealed record ContextLine(string Source, string Translation);

public sealed record TranslationRequest(string SourceText, GameProfile Profile, IReadOnlyList<ContextLine> PreviousLines);

public sealed record TranslationResult(string TranslatedText, string Provider, string Model);

public interface ITranslator
{
    /// <summary>Ném <see cref="TranslationException"/> khi lỗi key, quota, mạng...</summary>
    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);
}
