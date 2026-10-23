using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.EngineReads;

namespace Aw.Sindri.Features.ListEngineInstances;

/// <summary>'GET /sindri/engine/instances?state=&amp;stuck=' (API spec §3.8, E-30; query): the running instances as Temporal sees them.</summary>
/// <param name="State">Keeps only the instances in this state; null for every state.</param>
/// <param name="Stuck">'true' keeps only the stuck instances, 'false' only the others; null for both. Anything else is SND-API-013.</param>
public sealed record ListEngineInstancesRequest(string? State = null, string? Stuck = null) : IQuery<EngineInstanceList>;
