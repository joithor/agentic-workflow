using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Features.ApproveRecipe;

/// <summary>
/// The body of POST .../approve: a human's challenge and typed text (the same JSON a <see cref="ChallengeAnswer"/> is), or an agent's eval
/// files with no challenge.
/// </summary>
/// <param name="Challenge">The challenge a human redeems; null for an agent.</param>
/// <param name="Typed">The text the human typed.</param>
/// <param name="HeldMs">How long the human held the button, in the app.</param>
/// <param name="Evals">The recipe's eval files an agent carries.</param>
public sealed record ApproveRecipeBody(ChallengeId? Challenge = null, string Typed = "", long? HeldMs = null, IReadOnlyList<EvalFile>? Evals = null)
{
    /// <summary>The answer a human sent, or null when there is no challenge.</summary>
    /// <returns>The answer.</returns>
    public ChallengeAnswer? Answer() => Challenge is { } id ? new ChallengeAnswer(id, Typed, HeldMs) : null;
}
