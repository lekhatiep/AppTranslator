using AppTranslator.Core.Text;

namespace AppTranslator.Core.Pipeline;

/// <summary>
/// Chọn kết quả OCR đáng tin nhất trong nhiều lần đọc (nhiều khung hình và/hoặc nhiều engine):
/// lấy kết quả "ở giữa" — tổng khoảng cách chỉnh sửa theo từ tới các kết quả khác nhỏ nhất.
/// Lỗi ngẫu nhiên của một lần đọc ít khi trùng với lần khác nên bị loại.
/// Benchmark Stage 4: 5 cấu hình Windows OCR + PaddleOCR → phụ đề 91.0% (cấu hình đơn tốt nhất 82.6%).
/// </summary>
public static class OcrVote
{
    /// <summary>Kết quả rỗng (đọc không ra chữ) không được chọn trừ khi tất cả đều rỗng. Hòa thì lấy kết quả đứng trước.</summary>
    public static string SelectMedian(IReadOnlyList<string> candidates)
    {
        var tokenized = new List<string[]>();
        foreach (string candidate in candidates)
            tokenized.Add(candidate.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        int bestIndex = -1;
        int bestTotalDistance = int.MaxValue;
        for (int i = 0; i < tokenized.Count; i++)
        {
            if (tokenized[i].Length == 0)
                continue;

            int totalDistance = 0;
            for (int j = 0; j < tokenized.Count; j++)
            {
                if (i != j)
                    totalDistance += WordDistance.Between(tokenized[i], tokenized[j]);
            }

            if (totalDistance < bestTotalDistance)
            {
                bestTotalDistance = totalDistance;
                bestIndex = i;
            }
        }

        return bestIndex < 0 ? string.Empty : candidates[bestIndex];
    }
}
