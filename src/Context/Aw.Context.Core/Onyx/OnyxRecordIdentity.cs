using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Onyx;

/// <summary>Who an Onyx hit is across adapters (<see cref="OnyxRecordKeys"/>).</summary>
/// <param name="Canonical">Its canonical key.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Alias">A Linear identifier such as 'ENG-482' that names the same issue, or null.</param>
public sealed record OnyxRecordIdentity(RecordKey Canonical, RecordKind Kind, string? Alias);
