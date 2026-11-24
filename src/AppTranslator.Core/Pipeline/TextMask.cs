using AppTranslator.Core.Abstractions;

namespace AppTranslator.Core.Pipeline;

/// <summary>
/// Bản đồ thô "ô nào có chữ" của vùng phụ đề, dùng để biết chữ đã đổi hay đứng yên mà không cần OCR.
/// Chia ảnh thành ô 8×8 px; ô có chữ khi có ít nhất 2 điểm sáng (chữ phụ đề trắng/vàng) trong 16 điểm lấy mẫu.
/// Gộp theo ô để nhiễu nén video ở mép chữ không làm khung sau khác khung trước.
/// </summary>
public sealed class TextMask
{
    private const int CellSize = 8;
    private const int SampleStep = 2;
    private const int MinBrightSamplesPerCell = 2;

    /// <summary>
    /// Độ sáng (0–255) mặc định từ đây trở lên coi là điểm chữ: phụ đề trắng ~230–255.
    /// Chữ lựa chọn hội thoại vàng sẫm hơn (sáng nhất ~165, đo trên ảnh mẫu) nên dùng ngưỡng thấp hơn.
    /// </summary>
    public const int DefaultMinTextLuma = 200;

    // Ít hơn tỉ lệ ô này thì coi như không có chữ (vài đốm sáng lẻ của nền).
    private const double MinTextCellRatio = 0.005;

    private readonly bool[] _cells;

    private TextMask(int columns, int rows, bool[] cells)
    {
        Columns = columns;
        Rows = rows;
        _cells = cells;

        int textCells = 0;
        foreach (bool cell in cells)
        {
            if (cell)
                textCells++;
        }

        TextCellCount = textCells;
    }

    public int Columns { get; }

    public int Rows { get; }

    public int TextCellCount { get; }

    public bool HasText => _cells.Length > 0 && (double)TextCellCount / _cells.Length >= MinTextCellRatio;

    /// <summary>Ảnh BGRA (vùng chữ đã cắt, chưa tiền xử lý).</summary>
    public static TextMask FromFrame(Frame image, int minTextLuma = DefaultMinTextLuma)
    {
        int columns = image.Width / CellSize;
        int rows = image.Height / CellSize;
        var cells = new bool[columns * rows];

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
                cells[row * columns + column] = CountBrightSamples(image, column * CellSize, row * CellSize, minTextLuma) >= MinBrightSamplesPerCell;
        }

        return new TextMask(columns, rows, cells);
    }

    /// <summary>
    /// Tỉ lệ ô khác nhau trên số ô có chữ ở ít nhất một bên (0 = giống hệt, 1 = khác hẳn).
    /// Cả hai đều không có chữ thì là 0; khác kích thước (đổi vùng/cửa sổ) thì là 1.
    /// </summary>
    public double ChangeRatio(TextMask other)
    {
        if (other.Columns != Columns || other.Rows != Rows)
            return 1.0;

        int differentCells = 0;
        int textCellsInEither = 0;
        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] || other._cells[i])
                textCellsInEither++;

            if (_cells[i] != other._cells[i])
                differentCells++;
        }

        if (textCellsInEither == 0)
            return 0.0;

        return (double)differentCells / textCellsInEither;
    }

    private static int CountBrightSamples(Frame image, int left, int top, int minTextLuma)
    {
        int count = 0;
        for (int y = top; y < top + CellSize; y += SampleStep)
        {
            int rowStart = y * image.Stride;
            for (int x = left; x < left + CellSize; x += SampleStep)
            {
                int offset = rowStart + x * 4;
                int blue = image.Pixels[offset];
                int green = image.Pixels[offset + 1];
                int red = image.Pixels[offset + 2];
                int luma = (red * 299 + green * 587 + blue * 114) / 1000;
                if (luma >= minTextLuma)
                    count++;
            }
        }

        return count;
    }
}
