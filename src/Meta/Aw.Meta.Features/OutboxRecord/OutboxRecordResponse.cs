using Aw.Meta.Core.Obligations;

namespace Aw.Meta.Features.OutboxRecord;

/// <summary>The OutboxRecord response.</summary>
/// <param name="Recorded">Whether the send was recorded; false for any session but the meta.</param>
/// <param name="Closed">The obligations this send closed.</param>
public sealed record OutboxRecordResponse(bool Recorded, IReadOnlyList<Obligation> Closed);
