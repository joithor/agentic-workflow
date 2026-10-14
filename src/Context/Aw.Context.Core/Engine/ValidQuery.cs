namespace Aw.Context.Core.Engine;

/// <summary>A checked text question: its search terms and its budget after the cap.</summary>
/// <param name="Terms">The search terms (letters and digits, lower-case, at most <see cref="QueryRules.MaxTerms"/>).</param>
/// <param name="Budget">The token budget, capped at 'context.maxBudget'.</param>
public sealed record ValidQuery(IReadOnlyList<string> Terms, int Budget);
