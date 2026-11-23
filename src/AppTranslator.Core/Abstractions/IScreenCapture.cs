namespace AppTranslator.Core.Abstractions;

public interface IScreenCapture
{
    /// <summary>Chụp toàn bộ vùng client của cửa sổ; null nếu cửa sổ không còn hoặc chưa có khung hình.</summary>
    Task<Frame?> CaptureAsync(nint windowHandle, CancellationToken cancellationToken = default);
}
