namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>One model an agent offers, as a reader found it (M-3; the shared model type of M-1 replaces this when it merges).</summary>
/// <param name="Id">The id the agent's CLI accepts after its model flag.</param>
/// <param name="DisplayName">The name the agent shows for it.</param>
/// <param name="Priority">The agent's own order number, or null when it gives none (Codex: a larger number is cheaper).</param>
/// <param name="Source">Where the entry came from ('codex debug models', 'catalog').</param>
internal sealed record DiscoveredModel(string Id, string DisplayName, int? Priority, string Source);
