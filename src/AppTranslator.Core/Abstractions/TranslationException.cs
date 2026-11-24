namespace AppTranslator.Core.Abstractions;

public enum TranslationErrorKind
{
    /// <summary>Chưa nhập key, key sai hoặc bị thu hồi.</summary>
    InvalidKey,

    /// <summary>Hết lượt gọi (quota) hoặc gọi quá nhanh.</summary>
    QuotaExceeded,

    /// <summary>Dịch vụ phản hồi quá chậm.</summary>
    Timeout,

    /// <summary>Không kết nối được (mất mạng, DNS...).</summary>
    Network,

    /// <summary>Dịch vụ lỗi tạm thời (5xx).</summary>
    ServiceUnavailable,

    /// <summary>Lỗi khác: phản hồi không đọc được, bị từ chối nội dung...</summary>
    Other,
}

/// <summary>Lỗi khi dịch, kèm loại lỗi để hiện trạng thái ngắn cho người chơi (FR-13). Message là tiếng Việt, ngắn.</summary>
public sealed class TranslationException(TranslationErrorKind kind, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public TranslationErrorKind Kind { get; } = kind;
}
