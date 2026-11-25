using AppTranslator.Core.Abstractions;

namespace AppTranslator.Core.Pipeline;

/// <summary>
/// FR-05: tìm lựa chọn đang được chọn nhờ ký hiệu ▶ (tam giác vàng nhỏ) ngay bên trái số thứ tự.
/// OCR không đọc ra ký hiệu này, và màu chữ không dùng được (lựa chọn chưa chọn cũng có thể vàng), nên đếm điểm ảnh vàng
/// trong dải bên trái mỗi dòng. Rẻ (vài nghìn điểm ảnh), chạy được mỗi khung để bám theo khi người chơi di chuyển lựa chọn.
/// </summary>
public static class SelectionMarker
{
    // Dải tìm bên trái dòng: rộng gấp đôi chiều cao dòng (▶ cao bằng ~1/3 dòng, cách số ~1/2 dòng).
    private const double SearchWidthPerRowHeight = 2.0;

    // Điểm ảnh "vàng": đỏ và xanh lá cao, xanh dương thấp hơn hẳn (▶ ở ảnh mẫu ~ (230, 170, 30)).
    private const int MinRed = 150;
    private const int MinGreen = 100;
    private const int MinRedMinusBlue = 80;

    // ▶ ở 1080p chiếm ~60 điểm ảnh; dưới mức này coi như không có.
    private const int MinMarkerPixels = 15;

    /// <summary>Chỉ số dòng có ▶, hoặc null nếu không dòng nào có (hoặc hai dòng ngang nhau, không chắc).</summary>
    public static int? FindSelected(Frame image, IReadOnlyList<PixelRect> rows)
    {
        int bestIndex = -1;
        int bestCount = 0;
        int secondCount = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            int count = CountMarkerPixels(image, rows[i]);
            if (count > bestCount)
            {
                secondCount = bestCount;
                bestCount = count;
                bestIndex = i;
            }
            else if (count > secondCount)
            {
                secondCount = count;
            }
        }

        // Dòng thắng phải rõ ràng hơn dòng nhì (nền vàng như đuốc, nắng có thể lẫn vào vài dòng).
        if (bestCount < MinMarkerPixels || bestCount < 2 * secondCount)
            return null;

        return bestIndex;
    }

    private static int CountMarkerPixels(Frame image, PixelRect row)
    {
        int right = Math.Clamp(row.X, 0, image.Width);
        int left = Math.Clamp(row.X - (int)(row.Height * SearchWidthPerRowHeight), 0, image.Width);
        int top = Math.Clamp(row.Y, 0, image.Height);
        int bottom = Math.Clamp(row.Y + row.Height, 0, image.Height);

        int count = 0;
        for (int y = top; y < bottom; y++)
        {
            int rowStart = y * image.Stride;
            for (int x = left; x < right; x++)
            {
                int offset = rowStart + x * 4;
                int blue = image.Pixels[offset];
                int green = image.Pixels[offset + 1];
                int red = image.Pixels[offset + 2];
                if (red >= MinRed && green >= MinGreen && red - blue >= MinRedMinusBlue)
                    count++;
            }
        }

        return count;
    }
}
