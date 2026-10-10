namespace Aw.Architecture.Tests.Rules;

/// <summary>One rule violation: the rule id, what was found and the fix (spec §8: every failure names all three).</summary>
/// <param name="RuleId">The stable rule id, e.g. ARCH-DIR-001.</param>
/// <param name="Found">What the rule found, naming the types involved.</param>
/// <param name="Fix">How to fix it.</param>
public sealed record Violation(string RuleId, string Found, string Fix)
{
    /// <inheritdoc/>
    public override string ToString() => $"{RuleId}: {Found}. Fix: {Fix}";
}
