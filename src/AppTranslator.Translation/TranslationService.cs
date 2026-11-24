using System.Diagnostics;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Profiles;
using AppTranslator.Core.Text;

namespace AppTranslator.Translation;

/// <param name="FromCache">Lấy từ cache hoặc trùng câu ngay trước: không gọi AI.</param>
/// <param name="Elapsed">Thời gian của bước dịch (tra cache + gọi AI).</param>
public sealed record TranslatedLine(string SourceText, string TranslatedText, bool FromCache, TimeSpan Elapsed);

/// <summary>
/// Dịch một câu: bỏ câu rác → trùng câu trước thì dùng lại → tra cache (FR-10) → gọi AI kèm vài câu trước làm ngữ cảnh (FR-09).
/// Gọi lần lượt từ một luồng (vòng đọc phụ đề), không dùng song song.
/// </summary>
public sealed class TranslationService
{
    private readonly ITranslator _translator;
    private readonly ITranslationCache? _cache;
    private readonly List<ContextLine> _context = [];
    private string? _contextProfileId;

    /// <param name="cache">null = không dùng cache (vd bộ dịch giả, để bản dịch giả không lẫn vào cache thật).</param>
    public TranslationService(ITranslator translator, ITranslationCache? cache)
    {
        _translator = translator;
        _cache = cache;
    }

    /// <summary>Trả về null nếu câu trông như chữ rác (không gửi đi dịch). Ném <see cref="TranslationException"/> khi lỗi.</summary>
    public async Task<TranslatedLine?> TranslateAsync(GameProfile profile, string sourceText, CancellationToken cancellationToken = default)
    {
        if (OcrQuality.LooksLikeGarbage(sourceText))
            return null;

        // Đổi game thì ngữ cảnh cũ không còn đúng.
        if (_contextProfileId != profile.Id)
        {
            _context.Clear();
            _contextProfileId = profile.Id;
        }

        var stopwatch = Stopwatch.StartNew();
        var previous = _context.Count > 0 ? _context[^1] : null;
        if (previous is not null && TextNormalizer.ForComparison(previous.Source) == TextNormalizer.ForComparison(sourceText))
            return new TranslatedLine(sourceText, previous.Translation, FromCache: true, stopwatch.Elapsed);

        if (_cache is not null)
        {
            var cached = await _cache.GetAsync(profile, sourceText, cancellationToken);
            if (cached is not null)
            {
                Remember(profile, sourceText, cached.TranslatedText);
                return new TranslatedLine(sourceText, cached.TranslatedText, FromCache: true, stopwatch.Elapsed);
            }
        }

        var request = new TranslationRequest(sourceText, profile, _context.ToList());
        var result = await _translator.TranslateAsync(request, cancellationToken);

        if (_cache is not null)
            await _cache.SetAsync(profile, sourceText, result, cancellationToken);

        Remember(profile, sourceText, result.TranslatedText);
        return new TranslatedLine(sourceText, result.TranslatedText, FromCache: false, stopwatch.Elapsed);
    }

    private void Remember(GameProfile profile, string sourceText, string translation)
    {
        _context.Add(new ContextLine(sourceText, translation));
        while (_context.Count > Math.Max(0, profile.Style.ContextLines))
            _context.RemoveAt(0);
    }
}
