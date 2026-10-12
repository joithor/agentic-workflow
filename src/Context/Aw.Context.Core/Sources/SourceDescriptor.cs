namespace Aw.Context.Core.Sources;

/// <summary>What a source is (spec §5.1 Describe).</summary>
/// <param name="Id">Its id.</param>
/// <param name="Kind">The layer it feeds.</param>
/// <param name="Freshness">Live or snapshot.</param>
/// <param name="SupportsAsOf">True when it can cut its answers to a past time; otherwise as-of hits come back leaky.</param>
public sealed record SourceDescriptor(SourceId Id, SourceKind Kind, SourceFreshness Freshness, bool SupportsAsOf);
