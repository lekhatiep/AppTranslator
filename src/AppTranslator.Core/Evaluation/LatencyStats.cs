namespace AppTranslator.Core.Evaluation;

public static class LatencyStats
{
    /// <summary>
    /// Phân vị theo cách "nearest rank": giá trị nhỏ nhất mà ít nhất p% số đo nhỏ hơn hoặc bằng nó (AC-05 dùng p90).
    /// Danh sách rỗng thì null.
    /// </summary>
    public static double? Percentile(IReadOnlyCollection<double> values, double percent)
    {
        if (values.Count == 0)
            return null;

        var sorted = new List<double>(values);
        sorted.Sort();
        int rank = (int)Math.Ceiling(percent / 100 * sorted.Count);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
    }
}
