namespace Aw.CodeIndex.Core.Model;

/// <summary>A layer's row (the 'layers' table, spec §4.2).</summary>
/// <param name="Layer">Which layer.</param>
/// <param name="Stamp">What the layer was built with; a different stamp means it is rebuilt (the structure stamp, the graph's commit, the embedding model).</param>
/// <param name="State">Its status.</param>
/// <param name="Code">The SND-CODEIDX code when unavailable, or null.</param>
/// <param name="Detail">Codes and counts only ('31 of 100 embedded'), never tool output or text.</param>
/// <param name="BuiltAt">When it was last built.</param>
public sealed record LayerRecord(LayerName Layer, string Stamp, LayerState State, string? Code, string Detail, DateTimeOffset BuiltAt);
