namespace Aw.Sindri.Features.Scoping;

/// <summary>What the rules of one run share: the facts and the ids citations resolve to.</summary>
/// <param name="facts">The facts of the run.</param>
internal sealed class ScopeContext(ScopeFacts facts)
{
    /// <summary>The facts.</summary>
    internal ScopeFacts Facts { get; } = facts;

    /// <summary>The ids a citation can resolve to.</summary>
    internal ScopeIds Ids { get; } = new(facts);
}
