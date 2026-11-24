using System.Runtime.InteropServices;
using AppTranslator.Core.Abstractions;
using OpenCvSharp;

namespace AppTranslator.Imaging;

/// <summary>Chuyển đổi giữa <see cref="Frame"/> (BGRA) và Mat của OpenCV; đọc ảnh từ file.</summary>
public static class FrameConverter
{
    private const int BytesPerPixel = 4;

    public static Mat ToMat(Frame frame)
    {
        var mat = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
        for (int row = 0; row < frame.Height; row++)
            Marshal.Copy(frame.Pixels, row * frame.Stride, mat.Ptr(row), frame.Width * BytesPerPixel);

        return mat;
    }

    /// <summary>Đổi Mat (1, 3 hoặc 4 kênh) sang Frame BGRA.</summary>
    public static Frame ToFrame(Mat mat)
    {
        using var bgra = ToBgra(mat);
        int stride = bgra.Width * BytesPerPixel;
        var pixels = new byte[stride * bgra.Height];
        for (int row = 0; row < bgra.Height; row++)
            Marshal.Copy(bgra.Ptr(row), pixels, row * stride, stride);

        return new Frame(pixels, bgra.Width, bgra.Height, stride, DateTimeOffset.Now);
    }

    public static Frame Load(string imagePath)
    {
        using var mat = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (mat.Empty())
            throw new FileNotFoundException("Không đọc được ảnh", imagePath);

        return ToFrame(mat);
    }

    public static void Save(Frame frame, string imagePath)
    {
        using var mat = ToMat(frame);
        Cv2.ImWrite(imagePath, mat);
    }

    public static Frame Crop(Frame frame, PixelRect area)
    {
        using var mat = ToMat(frame);
        using var cropped = new Mat(mat, new Rect(area.X, area.Y, area.Width, area.Height));
        return ToFrame(cropped);
    }

    /// <summary>Thu nhỏ (giữ tỉ lệ) nếu cạnh dài vượt quá maxDimension; ảnh đã vừa thì trả lại nguyên ảnh.</summary>
    public static Frame ScaleDownToFit(Frame frame, int maxDimension)
    {
        int longestSide = Math.Max(frame.Width, frame.Height);
        if (longestSide <= maxDimension)
            return frame;

        double scale = (double)maxDimension / longestSide;
        using var mat = ToMat(frame);
        using var resized = new Mat();
        Cv2.Resize(mat, resized, new Size(), scale, scale, InterpolationFlags.Area);
        return ToFrame(resized);
    }

    private static Mat ToBgra(Mat mat)
    {
        var bgra = new Mat();
        if (mat.Channels() == 1)
            Cv2.CvtColor(mat, bgra, ColorConversionCodes.GRAY2BGRA);
        else if (mat.Channels() == 3)
            Cv2.CvtColor(mat, bgra, ColorConversionCodes.BGR2BGRA);
        else
            mat.CopyTo(bgra);

        return bgra;
    }
}
