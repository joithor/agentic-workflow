using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>One layer of an index as a reader sees it.</summary>
/// <param name="Layer">Which layer.</param>
/// <param name="State">Its status.</param>
/// <param name="Code">The SND-CODEIDX code when unavailable, or null.</param>
/// <param name="Detail">Codes and counts only.</param>
public sealed record LayerStatus(LayerName Layer, LayerState State, string? Code, string Detail);
