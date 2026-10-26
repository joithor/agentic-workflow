namespace Aw.Sindri.Infrastructure.Launch;

#pragma warning disable MA0182 // until the launch probe fills TargetFacts.Models with it (M-3, after M-1's types)
/// <summary>Claude Code has no command that lists models (spec §3), so Sindri keeps the aliases its --model flag accepts, cheapest first.</summary>
internal static class ClaudeModels
{
    private const string Source = "catalog";

    /// <summary>The catalog entry's models.</summary>
    public static IReadOnlyList<DiscoveredModel> Catalog { get; } =
    [
        new("haiku", "Haiku", Priority: null, Source),
        new("sonnet", "Sonnet", Priority: null, Source),
        new("opus", "Opus", Priority: null, Source),
    ];
}
#pragma warning restore MA0182
