namespace AppTranslator.Core.Profiles;

public enum RegionType
{
    Subtitle,
    Choices,
}

/// <summary>Vùng chữ, tọa độ theo tỉ lệ 0–1 của cửa sổ nguồn.</summary>
public sealed record Region
{
    public required RegionType Type { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double W { get; init; }
    public double H { get; init; }
    public string? TextColor { get; init; }
    public int? MaxLines { get; init; }
    public string? SelectedMarker { get; init; }
}
