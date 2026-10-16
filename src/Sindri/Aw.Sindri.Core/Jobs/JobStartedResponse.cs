using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Jobs;

/// <summary>What a job-class command answers (API spec §3.2 'JobStarted' plus the links the context lane asked for): the start was accepted and the work runs outside the gate.</summary>
/// <param name="Accepted">The start command's id and the sequence of 'job.started'.</param>
/// <param name="JobId">The job.</param>
/// <param name="Links">Where to follow or cancel it.</param>
public sealed record JobStartedResponse(CommandAccepted Accepted, JobId JobId, JobLinks Links);
