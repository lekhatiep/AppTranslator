namespace AppTranslator.Core.Text;

/// <summary>Khoảng cách chỉnh sửa (Levenshtein) theo từ: số từ phải thêm, xóa hoặc thay để dãy này thành dãy kia.</summary>
public static class WordDistance
{
    public static int Between(IReadOnlyList<string> first, IReadOnlyList<string> second)
    {
        // Chỉ giữ hai hàng của bảng quy hoạch động.
        var previousRow = new int[second.Count + 1];
        var currentRow = new int[second.Count + 1];
        for (int j = 0; j <= second.Count; j++)
            previousRow[j] = j;

        for (int i = 1; i <= first.Count; i++)
        {
            currentRow[0] = i;
            for (int j = 1; j <= second.Count; j++)
            {
                int substitutionCost = first[i - 1] == second[j - 1] ? 0 : 1;
                int deletion = previousRow[j] + 1;
                int insertion = currentRow[j - 1] + 1;
                int substitution = previousRow[j - 1] + substitutionCost;
                currentRow[j] = Math.Min(Math.Min(deletion, insertion), substitution);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[second.Count];
    }
}
