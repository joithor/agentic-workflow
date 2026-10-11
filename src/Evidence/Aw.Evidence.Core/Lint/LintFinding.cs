namespace Aw.Evidence.Core.Lint;

/// <summary>One lint rule hit.</summary>
/// <param name="Rule">The rule.</param>
/// <param name="Selector">The element (or 'a / b' for a pair).</param>
/// <param name="Detail">What was measured.</param>
public sealed record LintFinding(LintRule Rule, string Selector, string Detail);
