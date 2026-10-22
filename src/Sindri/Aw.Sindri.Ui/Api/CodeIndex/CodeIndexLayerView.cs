namespace Aw.Sindri.Ui.Api.CodeIndex;

/// <summary>One layer of an index.</summary>
/// <param name="Layer">'structure', 'clones', 'deps', 'graph' or 'embeddings'.</param>
/// <param name="State">'ok', 'unavailable', 'disabled' or 'pending'.</param>
/// <param name="Code">The SND-CODEIDX code when it is unavailable, or null.</param>
/// <param name="Detail">A short detail; embeddings say "N of M embedded".</param>
internal sealed record CodeIndexLayerView(string Layer, string State, string? Code, string Detail);
