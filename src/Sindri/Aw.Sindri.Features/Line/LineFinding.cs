namespace Aw.Sindri.Features.Line;

/// <summary>One finding of a design or plan check, in the shape #258's checks use: the rule, what it is about, what is wrong and what to change. A warning is shown and never changes an outcome.</summary>
/// <param name="Rule">The rule id, for example 'DSN-COV-001'.</param>
/// <param name="Item">What it is about (an id), or null for the whole fact.</param>
/// <param name="Message">What is wrong.</param>
/// <param name="Fix">What to change.</param>
/// <param name="Warn">True for a warning.</param>
internal sealed record LineFinding(string Rule, string? Item, string Message, string Fix, bool Warn = false);
