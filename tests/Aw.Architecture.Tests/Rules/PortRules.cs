namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// Every external technology sits behind a port (csharp.md Seams): Core, Features and the small shape's App.Commands use
/// no technology type, and only Composition uses Infrastructure (Host, including Program, and App.Commands).
/// </summary>
public static class PortRules
{
    /// <summary>A technology type in Core, Features or the small shape's App.Commands.</summary>
    public const string TechnologyId = "ARCH-PORT-001";

    /// <summary>Infrastructure used outside Infrastructure and Composition.</summary>
    public const string CompositionId = "ARCH-PORT-002";

    /// <summary>Checks both port rules.</summary>
    /// <param name="edges">The dependencies to check.</param>
    /// <returns>One violation per offending edge.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges) =>
        edges.Select(Judge).OfType<Violation>();

    private static Violation? Judge(Edge e) => e switch
    {
        { From.Layer: Layer.Core or Layer.Features or Layer.Commands } when Technology.Reaches(e) =>
            new Violation(TechnologyId, $"{e.Text}: {e.From.Layer} {Technology.Describe(e)}",
                "declare a port in Core (named for what the code needs), implement it in Infrastructure, and pick the adapter in Composition"),
        { From: { Layer: Layer.Host or Layer.Commands, IsComposition: false } from, To: { Layer: Layer.Infrastructure } to }
            when from.Root == to.Root =>
            new Violation(CompositionId, $"{e.Text}: {from.Layer} uses Infrastructure outside Composition",
                "resolve the port interface instead; only Composition calls the Infrastructure Add…() methods"),
        _ => null,
    };
}
