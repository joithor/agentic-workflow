namespace Aw.Sindri.Core.EngineReads;

/// <summary>'GET /sindri/engine/instances' (API spec §3.8, E-30).</summary>
/// <param name="Instances">The instances the filters keep, by address.</param>
/// <param name="Pending">The counts over every running instance, whatever the filters.</param>
/// <param name="AsOf">When this was read.</param>
public sealed record EngineInstanceList(IReadOnlyList<EngineInstance> Instances, EnginePending Pending, DateTimeOffset AsOf);
