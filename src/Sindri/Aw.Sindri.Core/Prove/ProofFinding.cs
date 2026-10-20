namespace Aw.Sindri.Core.Prove;

/// <summary>One proof rule that did not hold, in the line's findings shape (scoping spec §8: rule, item, message, fix; severity 'fail').</summary>
/// <param name="Rule">The rule id, for example 'PRV-ACC-001'.</param>
/// <param name="Item">The acceptance item it is about; null when it is about the whole proof.</param>
/// <param name="Message">What was found.</param>
/// <param name="Fix">How to fix it.</param>
public sealed record ProofFinding(string Rule, string? Item, string Message, string Fix);
