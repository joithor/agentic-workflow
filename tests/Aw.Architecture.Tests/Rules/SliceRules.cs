namespace Aw.Architecture.Tests.Rules;

/// <summary>Slice isolation (spec §4b.1): one slice never references another slice's types.</summary>
public static class SliceRules
{
    /// <summary>A Features slice depends on another slice.</summary>
    public const string Id = "ARCH-SLICE-001";

    /// <summary>Checks that no Features slice depends on another slice of the same package.</summary>
    /// <param name="edges">The dependencies to check.</param>
    /// <returns>One violation per offending edge.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges) =>
        edges.Select(e => e is { From: { Layer: Layer.Features, Slice: { } a } from, To: { Layer: Layer.Features, Slice: { } b } to }
                && from.Root == to.Root && a != b
                ? new Violation(Id, $"{e.Text}: slice {a} uses slice {b}",
                    "move the shared logic into Core as a domain service or port (the second use is the trigger, §6.2)")
                : null)
            .OfType<Violation>();
}
