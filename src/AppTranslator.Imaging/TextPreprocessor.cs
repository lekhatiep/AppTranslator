using AppTranslator.Core.Abstractions;
using OpenCvSharp;

namespace AppTranslator.Imaging;

public enum PreprocessMode
{
    /// <summary>Giữ nguyên ảnh cắt.</summary>
    None,

    /// <summary>Phóng to 2 lần: chữ phụ đề nhỏ (cao ~25px) dễ đọc hơn với OCR.</summary>
    Upscale2x,

    /// <summary>Phóng to 2 lần rồi chỉ giữ điểm ảnh màu chữ (trắng hoặc vàng), ra chữ đen trên nền trắng.</summary>
    TextColorMask2x,

    /// <summary>Làm nét (unsharp mask): bù nhòe do nén video/stream. Tốt nhất cho Windows OCR ở benchmark lần 3.</summary>
    Sharpen,
}

/// <summary>Tiền xử lý vùng chữ trước khi OCR (Stage 3 benchmark để chọn cách tốt nhất).</summary>
public static class TextPreprocessor
{
    private const double UpscaleFactor = 2.0;

    // Unsharp mask: ảnh + (ảnh − ảnh mờ) × SharpenAmount.
    private const double SharpenBlurSigma = 1.0;
    private const double SharpenAmount = 1.0;

    // Ngưỡng HSV của OpenCV (H 0–179, S và V 0–255), chỉnh theo phụ đề và lựa chọn Witcher 3.
    // Chữ trắng: rất sáng, gần như không có màu.
    private const int WhiteMinValue = 170;
    private const int WhiteMaxSaturation = 60;
    // Chữ vàng của lựa chọn hội thoại.
    private const int YellowMinHue = 15;
    private const int YellowMaxHue = 35;
    private const int YellowMinSaturation = 80;
    private const int YellowMinValue = 150;

    public static Frame Apply(Frame image, PreprocessMode mode)
    {
        if (mode == PreprocessMode.None)
            return image;

        using var source = FrameConverter.ToMat(image);
        if (mode == PreprocessMode.Sharpen)
            return Sharpen(source);

        using var upscaled = new Mat();
        Cv2.Resize(source, upscaled, new Size(), UpscaleFactor, UpscaleFactor, InterpolationFlags.Cubic);

        if (mode == PreprocessMode.Upscale2x)
            return FrameConverter.ToFrame(upscaled);

        using var mask = CreateTextColorMask(upscaled);
        using var blackTextOnWhite = new Mat();
        Cv2.BitwiseNot(mask, blackTextOnWhite);
        return FrameConverter.ToFrame(blackTextOnWhite);
    }

    private static Frame Sharpen(Mat bgra)
    {
        using var blurred = new Mat();
        using var sharpened = new Mat();
        Cv2.GaussianBlur(bgra, blurred, new Size(0, 0), SharpenBlurSigma);
        Cv2.AddWeighted(bgra, 1 + SharpenAmount, blurred, -SharpenAmount, 0, sharpened);
        return FrameConverter.ToFrame(sharpened);
    }

    private static Mat CreateTextColorMask(Mat bgra)
    {
        using var bgr = new Mat();
        using var hsv = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        Cv2.CvtColor(bgr, hsv, ColorConversionCodes.BGR2HSV);

        using var white = new Mat();
        using var yellow = new Mat();
        Cv2.InRange(hsv, new Scalar(0, 0, WhiteMinValue), new Scalar(179, WhiteMaxSaturation, 255), white);
        Cv2.InRange(hsv, new Scalar(YellowMinHue, YellowMinSaturation, YellowMinValue), new Scalar(YellowMaxHue, 255, 255), yellow);

        var mask = new Mat();
        Cv2.BitwiseOr(white, yellow, mask);
        return mask;
    }
}
