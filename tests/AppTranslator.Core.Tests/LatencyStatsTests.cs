using AppTranslator.Core.Evaluation;

namespace AppTranslator.Core.Tests;

public class LatencyStatsTests
{
    [Fact]
    public void Percentile_NearestRank()
    {
        double[] values = [500, 100, 400, 200, 300, 600, 700, 800, 900, 1000];

        Assert.Equal(500, LatencyStats.Percentile(values, 50));
        Assert.Equal(900, LatencyStats.Percentile(values, 90));
        Assert.Equal(1000, LatencyStats.Percentile(values, 100));
        Assert.Equal(100, LatencyStats.Percentile(values, 1));
    }

    [Fact]
    public void Percentile_SingleAndEmpty()
    {
        Assert.Equal(42, LatencyStats.Percentile([42.0], 90));
        Assert.Null(LatencyStats.Percentile([], 90));
    }
}
