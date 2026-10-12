namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>The throwaway interpreter's input.</summary>
/// <param name="Item">The work item.</param>
/// <param name="Recipe">The recipe to walk.</param>
internal sealed record SpikeRun(string Item, SpikeRecipe Recipe);
