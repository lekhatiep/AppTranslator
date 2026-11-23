namespace AppTranslator.Core.Profiles;

public sealed record GameProfile
{
    public required string Id { get; init; }
    public required string GameName { get; init; }
    public string? WindowTitleHint { get; init; }
    public IReadOnlyList<Region> Regions { get; init; } = [];
    public StyleOptions Style { get; init; } = new();
    public IReadOnlyList<string> KeepNames { get; init; } = [];
    public IReadOnlyList<GlossaryEntry> Glossary { get; init; } = [];
    public int Version { get; init; } = 1;
}

public sealed record GlossaryEntry(string En, string Vi);
