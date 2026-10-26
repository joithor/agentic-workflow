namespace Aw.Sindri.Core.Launch;

/// <summary>Puts an agent's models in order and cuts them into the three bands (model-pick spec §3, "Tiers"). Pure.</summary>
public static class ModelBands
{
    /// <summary>
    /// The models cheapest first with their bands. Order: by <see cref="ModelFacts.Order"/>, ties and the listed order kept; models with no order go last, in the order given.
    /// With N models, cheap is the first ceil(N/3), strong the last ceil(N/3) and standard what is left between; when nothing is left between, standard is every model.
    /// </summary>
    /// <param name="models">The agent's models in any order.</param>
    /// <returns>The ranked models, cheapest first.</returns>
    public static IReadOnlyList<RankedModel> Rank(IReadOnlyList<ModelFacts> models) => throw new NotSupportedException();
}
