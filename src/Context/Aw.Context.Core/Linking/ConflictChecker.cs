using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Linking;

/// <summary>
/// Intent-vs-intent conflicts (spec §8.9, A-F13): two current decisions on the same node that disagree, model-checked once per pair.
/// </summary>
public static class ConflictChecker
{
    /// <summary>The pairs to ask about: each added decision against every other current decision on a node it decides, unless the pair was already checked.</summary>
    /// <param name="layer">The intent layer after the added decisions were saved.</param>
    /// <param name="added">The decisions just saved.</param>
    /// <returns>The pairs, in key order, with the node they share.</returns>
    public static IReadOnlyList<(DecisionRow First, DecisionRow Second, NodeKey Node)> Pairs(IntentLayer layer, IReadOnlyList<DecisionRow> added) => throw new NotSupportedException("S-5B slice 4");

    /// <summary>The prompt that asks whether two decisions contradict each other.</summary>
    /// <param name="first">One decision.</param>
    /// <param name="second">The other.</param>
    /// <returns>The prompt.</returns>
    public static string Prompt(DecisionRow first, DecisionRow second) => throw new NotSupportedException("S-5B slice 4");

    /// <summary>Reads the model's verdict: JSON '{"verdict":"contradicts|agrees|supersedes"}'.</summary>
    /// <param name="answer">The model's text.</param>
    /// <returns>The verdict, or null when the answer isn't one.</returns>
    public static ConflictVerdict? Read(string answer) => throw new NotSupportedException("S-5B slice 4");
}
