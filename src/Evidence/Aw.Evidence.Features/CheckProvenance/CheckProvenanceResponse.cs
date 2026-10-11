using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Features.CheckProvenance;

/// <summary>The database's provenance.</summary>
/// <param name="Provenance">Seeded only when a seed row exists and every profile email is on a seed domain.</param>
/// <param name="Reason">Why.</param>
public sealed record CheckProvenanceResponse(DbProvenance Provenance, string Reason);
