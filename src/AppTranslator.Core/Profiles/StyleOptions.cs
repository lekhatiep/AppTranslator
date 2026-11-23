namespace AppTranslator.Core.Profiles;

public sealed record StyleOptions
{
    public string? Tone { get; init; }
    public string? Addressing { get; init; }
    public int ContextLines { get; init; } = 3;
}
