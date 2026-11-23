namespace AppTranslator.Core.Abstractions;

public interface IScreenCapture
{
    /// <summary>
    /// Chụp toàn bộ khung cửa sổ (theo DWM extended frame bounds, giống phần Windows.Graphics.Capture chụp);
    /// null nếu cửa sổ không còn hoặc chưa có khung hình.
    /// </summary>
    Task<Frame?> CaptureAsync(nint windowHandle, CancellationToken cancellationToken = default);
}
