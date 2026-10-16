using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetInstanceEvents;

/// <summary>
/// 'workflow events &lt;instance&gt;' (engine spec §6.3; README X16) and 'GET /workflow/instances/{instanceId}/events?after=':
/// the instance's events, read from the ledger (the instance-level filter of the item timeline), after a cursor.
/// </summary>
/// <param name="Instance">The instance: its id ('WFI-…'), its address, or a bare item for the root of its latest attempt.</param>
/// <param name="After">The ledger sequence to read after (0 for all).</param>
public sealed record GetInstanceEventsRequest(string Instance, long After = 0) : IQuery<GetInstanceEventsResponse>;
