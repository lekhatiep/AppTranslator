using AppTranslator.Core.Profiles;

namespace AppTranslator.Core.Abstractions;

public interface ITranslationCache
{
    Task<TranslationResult?> GetAsync(GameProfile profile, string sourceText, CancellationToken cancellationToken = default);

    Task SetAsync(GameProfile profile, string sourceText, TranslationResult result, CancellationToken cancellationToken = default);

    Task ClearAsync(string profileId, CancellationToken cancellationToken = default);
}
