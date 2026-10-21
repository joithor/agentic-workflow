namespace Aw.CodeIndex.Core.Model;

/// <summary>An index's meta and layers, read without opening anything else (the pointer and the file header: a few small reads).</summary>
/// <param name="Meta">The header.</param>
/// <param name="Layers">One row per layer the build recorded.</param>
public sealed record IndexHeader(IndexMeta Meta, IReadOnlyList<LayerRecord> Layers);
