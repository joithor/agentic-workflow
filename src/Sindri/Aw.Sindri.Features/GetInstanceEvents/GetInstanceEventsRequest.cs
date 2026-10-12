using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.GetInstanceEvents;

/// <summary>
/// 'workflow events &lt;address&gt;' (engine spec §6.3; README X16): the instance's events, read from the ledger (the
/// instance-level filter of the item timeline), after a cursor.
/// </summary>
/// <param name="Address">The instance.</param>
/// <param name="After">The ledger sequence to read after (0 for all).</param>
public sealed record GetInstanceEventsRequest(InstanceAddress Address, long After = 0) : IQuery<GetInstanceEventsResponse>;
