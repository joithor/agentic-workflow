using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.Jobs;

/// <summary>What recording a start gives back.</summary>
/// <param name="Grant">The lease grant the event was written under.</param>
/// <param name="Sequence">The sequence of 'job.started'.</param>
/// <param name="Cause">Its envelope id.</param>
internal sealed record RecordedStart(LeaseGrant Grant, long Sequence, EventId Cause);
