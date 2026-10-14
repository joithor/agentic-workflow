using Aw.Context.Core.Map;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What an import showed or did.</summary>
/// <param name="Preview">The checked file: its repo, kind, content hash and counts.</param>
/// <param name="Imported">The new current version, or null for a preview.</param>
/// <param name="Sequence">The 'context.map.imported' event's ledger sequence, or null for a preview.</param>
public sealed record ImportContextMapResponse(MapFilePreview Preview, MapImported? Imported, long? Sequence);
