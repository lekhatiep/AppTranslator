using System.Windows;
using AppTranslator.App.Pipeline;
using AppTranslator.Capture.Windows;
using AppTranslator.Core.Abstractions;
using AppTranslator.Core.Pipeline;
using AppTranslator.Core.Profiles;
using AppTranslator.Imaging;
using AppTranslator.Storage;
using Microsoft.Extensions.Logging;

namespace AppTranslator.App;

/// <summary>
/// FR-07 (best effort): phím tắt → khoanh vùng bất kỳ → OCR + dịch một lần → popup.
/// Popup đóng khi bấm lại phím tắt, click vào popup, hoặc khi chữ trong vùng đổi (vd đóng bảng vật phẩm).
/// </summary>
public partial class MainViewModel
{
    // Chu kỳ kiểm tra vùng còn giữ nguyên chữ không (không cần nhanh như phụ đề).
    private static readonly TimeSpan AreaWatchInterval = TimeSpan.FromMilliseconds(500);

    // Khác quá tỉ lệ này so với lúc khoanh thì coi là chữ đã đổi (giống ngưỡng đổi câu của phụ đề).
    private const double AreaChangeThreshold = 0.3;

    // Chữ trong vùng thủ công có thể trắng hoặc vàng sẫm: dùng ngưỡng sáng thấp như vùng lựa chọn.
    private const int AreaTextMinLuma = 150;

    private TranslationPopupWindow? _areaPopup;
    private CancellationTokenSource? _areaWatchCancellation;
    private bool _isPickingArea;

    private async Task TranslateAreaAsync()
    {
        // Phím tắt bật/tắt: đang có popup thì bấm lại để đóng.
        if (_areaPopup is not null)
        {
            CloseAreaPopup();
            return;
        }

        if (_isPickingArea || SelectedProfile is null || SelectedWindow is null)
            return;

        var gameBounds = WindowFinder.GetBounds(SelectedWindow.Handle);
        var frame = await CaptureSelectedWindowAsync();
        if (frame is null || gameBounds is null)
            return;

        PixelRect? picked;
        _isPickingArea = true;
        try
        {
            var picker = new AreaPickerWindow(FrameImage.ToBitmapSource(frame), gameBounds.Value);
            picked = picker.ShowDialog() == true ? picker.Result : null;
        }
        finally
        {
            _isPickingArea = false;
        }

        if (picked is not { } area)
            return;

        var profile = SelectedProfile;
        var popup = new TranslationPopupWindow(ToScreen(area, frame, gameBounds.Value), gameBounds.Value);
        popup.Closed += (_, _) => OnAreaPopupClosed(popup);
        popup.SetText("Đang đọc chữ…", null);
        popup.Show();
        _areaPopup = popup;

        var crop = FrameConverter.Crop(frame, area);
        _areaWatchCancellation = new CancellationTokenSource();
        _ = WatchAreaAsync(SelectedWindow.Handle, area, TextMask.FromFrame(crop, AreaTextMinLuma), _areaWatchCancellation.Token);

        await ShowAreaTranslationAsync(popup, profile, crop);
    }

    private async Task ShowAreaTranslationAsync(TranslationPopupWindow popup, GameProfile profile, Frame crop)
    {
        try
        {
            string text = await _areaTextReader.ReadAsync(crop);
            if (text.Length == 0)
            {
                popup.SetText("Không đọc được chữ trong vùng này.", null);
                return;
            }

            popup.SetText("Đang dịch…", text);
            var line = await _subtitleTranslator.TranslateAreaAsync(profile, text);
            if (line is null)
            {
                popup.SetText("Chữ đọc ra không rõ, không dịch.", text);
                return;
            }

            popup.SetText(line.TranslatedText, text);
            _logger.LogInformation("Dịch vùng thủ công ({Source}, {Milliseconds:F0} ms): \"{Text}\" → \"{Translation}\"",
                line.FromCache ? "cache" : "AI", line.Elapsed.TotalMilliseconds, text, line.TranslatedText);
            await _history.AddAsync(new HistoryEntry(DateTimeOffset.Now, profile.Id, "Manual", text, line.TranslatedText, line.FromCache, null));
        }
        catch (TranslationException exception)
        {
            popup.SetText($"⚠ {exception.Message}", null);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Dịch vùng thủ công thất bại");
            popup.SetText($"⚠ Lỗi: {exception.Message}", null);
        }
    }

    /// <summary>Định kỳ chụp lại vùng; chữ trong vùng đổi (đóng bảng, sang trang) thì đóng popup.</summary>
    private async Task WatchAreaAsync(nint windowHandle, PixelRect area, TextMask original, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(AreaWatchInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var frame = await CurrentCapture.CaptureAsync(windowHandle, cancellationToken);
                if (frame is null || area.X + area.Width > frame.Width || area.Y + area.Height > frame.Height)
                    continue;

                var mask = await Task.Run(() => TextMask.FromFrame(FrameConverter.Crop(frame, area), AreaTextMinLuma), cancellationToken);
                if (mask.ChangeRatio(original) > AreaChangeThreshold)
                {
                    _logger.LogInformation("Chữ trong vùng thủ công đã đổi, đóng bản dịch");
                    CloseAreaPopup();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Popup đã đóng.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Theo dõi vùng thủ công dừng vì lỗi");
        }
    }

    private void CloseAreaPopup()
    {
        _areaPopup?.Close();
    }

    private void OnAreaPopupClosed(TranslationPopupWindow popup)
    {
        if (_areaPopup != popup)
            return;

        _areaPopup = null;
        _areaWatchCancellation?.Cancel();
        _areaWatchCancellation?.Dispose();
        _areaWatchCancellation = null;
    }

    /// <summary>Vùng trong ảnh chụp (pixel ảnh) → trên màn hình (pixel vật lý), co giãn nếu ảnh khác cỡ khung cửa sổ.</summary>
    private static PixelRect ToScreen(PixelRect area, Frame frame, PixelRect gameBounds)
    {
        double scaleX = (double)gameBounds.Width / frame.Width;
        double scaleY = (double)gameBounds.Height / frame.Height;
        return new PixelRect(
            gameBounds.X + (int)(area.X * scaleX),
            gameBounds.Y + (int)(area.Y * scaleY),
            (int)(area.Width * scaleX),
            (int)(area.Height * scaleY));
    }
}
