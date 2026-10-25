namespace Aw.Sindri.Core.Events;

/// <summary>One layer of a built index.</summary>
/// <param name="Name">The layer (structure, clones, deps, graph, embeddings).</param>
/// <param name="Status">ok, unavailable, disabled or pending.</param>
/// <param name="Code">The SND-CODEIDX code when unavailable, or null.</param>
public sealed record CodeIndexBuiltLayer(string Name, string Status, string? Code);
