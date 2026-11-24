using System.Globalization;
using System.Text;
using AppTranslator.Core.Profiles;
using AppTranslator.Imaging;

namespace AppTranslator.Benchmark;

internal sealed record BenchmarkResult(
    string Engine,
    PreprocessMode Mode,
    GroundTruthRow Row,
    string RecognizedText,
    double StrictAccuracy,
    double LooseAccuracy,
    double Milliseconds);

/// <summary>Xuất kết quả chi tiết ra CSV và in bảng tổng hợp theo engine × tiền xử lý × loại vùng.</summary>
internal static class BenchmarkReport
{
    public static void WriteCsv(IReadOnlyList<BenchmarkResult> results, string csvPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine("engine,preprocess,image,region,strict_accuracy,loose_accuracy,ms,expected,recognized");
        foreach (var result in results)
        {
            builder.Append(result.Engine).Append(',')
                .Append(result.Mode).Append(',')
                .Append(result.Row.ImageFile).Append(',')
                .Append(result.Row.Region).Append(',')
                .Append(Format(result.StrictAccuracy)).Append(',')
                .Append(Format(result.LooseAccuracy)).Append(',')
                .Append(result.Milliseconds.ToString("F0", CultureInfo.InvariantCulture)).Append(',')
                .Append(Quote(result.Row.Text)).Append(',')
                .AppendLine(Quote(result.RecognizedText));
        }

        // BOM để Excel mở đúng tiếng Việt.
        File.WriteAllText(csvPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    public static void PrintSummary(IReadOnlyList<BenchmarkResult> results)
    {
        Console.WriteLine();
        Console.WriteLine($"{"Engine",-14}{"Tiền xử lý",-17}{"Vùng",-10}{"Số câu",7}{"Đúng (chặt)",13}{"Đúng (lỏng)",13}{"Câu đúng 100%",15}{"ms TB",8}");

        var groups = new Dictionary<(string, PreprocessMode, RegionType), List<BenchmarkResult>>();
        foreach (var result in results)
        {
            var key = (result.Engine, result.Mode, result.Row.Region);
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(result);
        }

        foreach (var (key, list) in groups)
        {
            double strict = list.Average(r => r.StrictAccuracy);
            double loose = list.Average(r => r.LooseAccuracy);
            int perfect = list.Count(r => r.StrictAccuracy == 1);
            double milliseconds = list.Average(r => r.Milliseconds);
            Console.WriteLine($"{key.Item1,-14}{key.Item2,-17}{key.Item3,-10}{list.Count,7}{strict,13:P1}{loose,13:P1}{perfect + "/" + list.Count,15}{milliseconds,8:F0}");
        }
    }

    private static string Format(double value)
    {
        return value.ToString("F3", CultureInfo.InvariantCulture);
    }

    private static string Quote(string text)
    {
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
