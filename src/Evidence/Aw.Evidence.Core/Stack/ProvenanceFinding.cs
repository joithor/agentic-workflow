using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Core.Stack;

/// <summary>The database provenance check's result.</summary>
/// <param name="Provenance">Seeded or unknown.</param>
/// <param name="Reason">Why, for people.</param>
public sealed record ProvenanceFinding(DbProvenance Provenance, string Reason);
