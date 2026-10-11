namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// A Host uses a compose target's Features and Infrastructure only through their public static Add…() registration
/// methods (context spec §4). Without a compose edge such a use is ARCH-DIR-004.
/// </summary>
public static class ComposeRules
{
    /// <summary>A Host using anything but an Add…() registration method of a compose target's Features or Infrastructure.</summary>
    public const string Id = "ARCH-COMPOSE-001";

    /// <summary>Checks every Host edge into a compose target's Features or Infrastructure.</summary>
    /// <param name="edges">The dependencies to check.</param>
    /// <param name="compose">The declared compose edges.</param>
    /// <returns>One violation per offending edge.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<Edge> edges, IReadOnlyCollection<ComposeEdge> compose) =>
        throw new NotSupportedException();
}
