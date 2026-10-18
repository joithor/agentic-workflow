namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The inner policy of a plan: advice with a good error, not the boundary (sandbox spec SB2, §5).</summary>
/// <param name="Mode">Refuse or ask.</param>
/// <param name="Rules">The rules, in order.</param>
public sealed record InnerPolicy(InnerMode Mode, IReadOnlyList<InnerRule> Rules);
