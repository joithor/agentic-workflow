using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.EngineReads;

namespace Aw.Sindri.Features.GetEngineHistory;

/// <summary>'GET /sindri/engine/instances/{id}/history?page=' or '?address=' (API spec §3.8, E-30; query): one page of an instance's Temporal history, newest first.</summary>
/// <param name="Instance">The instance's 'WFI-' id (the route segment); null when an address is given.</param>
/// <param name="Address">The instance's address, when it is asked for by that; it wins over the id.</param>
/// <param name="Page">The page, from 1; null for the first. Anything else is SND-API-013.</param>
public sealed record GetEngineHistoryRequest(string? Instance = null, string? Address = null, string? Page = null) : IQuery<EngineHistory>;
