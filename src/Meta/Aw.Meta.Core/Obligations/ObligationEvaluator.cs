namespace Aw.Meta.Core.Obligations;

/// <summary>Derives the open obligations from the recorded facts. Nothing is stored: sending a message closes the obligation it answers.</summary>
public static class ObligationEvaluator
{
    /// <summary>Evaluates the open set.</summary>
    /// <param name="facts">The recorded facts.</param>
    /// <returns>The open obligations: replies in arrival order, then lane map, main, owner relay and watch.</returns>
    public static IReadOnlyList<Obligation> Evaluate(ObligationFacts facts) => throw new NotSupportedException("ObligationEvaluator: make ObligationEvaluatorTests pass");
}
