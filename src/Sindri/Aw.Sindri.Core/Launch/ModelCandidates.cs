namespace Aw.Sindri.Core.Launch;

/// <summary>The models a step may run on, before Jev is asked (model-pick spec §4.1). Pure.</summary>
public static class ModelCandidates
{
    /// <summary>
    /// Removes, in order: models below the floor; models above the cap, or all but the pin; the drafter's model for a challenger (all of the agent's models when the drafter's is unknown and the agent is shared);
    /// and, for an escalation, the failed model and everything at or below it. A model is attributed to the first rule that removes it.
    /// </summary>
    /// <param name="ladder">The agent's models from <see cref="ModelBands.Rank"/>, cheapest first.</param>
    /// <param name="constraints">What the step may use.</param>
    /// <returns>The candidates and the removals.</returns>
    public static ModelSelection For(IReadOnlyList<RankedModel> ladder, ModelConstraints constraints) => throw new NotSupportedException();
}
