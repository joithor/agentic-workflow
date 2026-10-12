namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>The test interpreter's input.</summary>
/// <param name="Item">The work item.</param>
/// <param name="Recipe">The recipe to walk.</param>
/// <param name="DeskByUpdate">
/// False (rev 8 §5.3): the desk Step is an async-completion activity. True (the spike's alternative): a short activity
/// records the desk task and the report arrives as the 'complete' Update (rev 8 §5.2), so no activity stays open.
/// </param>
internal sealed record SpikeRun(string Item, SpikeRecipe Recipe, bool DeskByUpdate = false);
