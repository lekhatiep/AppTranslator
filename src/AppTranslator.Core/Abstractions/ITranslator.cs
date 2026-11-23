using AppTranslator.Core.Profiles;

namespace AppTranslator.Core.Abstractions;

public sealed record TranslationRequest(string SourceText, GameProfile Profile, IReadOnlyList<string> PreviousLines);

public sealed record TranslationResult(string TranslatedText, string Provider, string Model);

public interface ITranslator
{
    Task<TranslationResult> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default);
}
