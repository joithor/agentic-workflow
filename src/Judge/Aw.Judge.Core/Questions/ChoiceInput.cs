namespace Aw.Judge.Core.Questions;

/// <summary>One choice of a choose call.</summary>
/// <param name="Name">The outcome name: a letter, then letters and digits (at most 32), never 'uncertain'.</param>
/// <param name="Criterion">When to pick it (1 to 400 characters).</param>
public sealed record ChoiceInput(string Name, string Criterion);
