using AppTranslator.Core.Profiles;
using Microsoft.VisualBasic.FileIO;

namespace AppTranslator.Benchmark;

internal sealed record GroundTruthRow(string ImageFile, RegionType Region, string Text);

/// <summary>Đọc ground-truth.csv (cột image, region, text; quy ước xem samples/witcher3/README.md).</summary>
internal static class GroundTruth
{
    public static List<GroundTruthRow> Load(string csvPath)
    {
        using var parser = new TextFieldParser(csvPath) { HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        parser.ReadFields(); // dòng tiêu đề

        var rows = new List<GroundTruthRow>();
        while (!parser.EndOfData)
        {
            string[] fields = parser.ReadFields() ?? [];
            if (fields.Length != 3)
                throw new InvalidDataException($"{csvPath} dòng {parser.LineNumber}: cần 3 cột image,region,text");

            var region = Enum.Parse<RegionType>(fields[1], ignoreCase: true);
            rows.Add(new GroundTruthRow(fields[0], region, fields[2]));
        }

        return rows;
    }
}
