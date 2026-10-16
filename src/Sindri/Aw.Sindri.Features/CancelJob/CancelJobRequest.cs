using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.CancelJob;

/// <summary>Cancel a running job (POST /sindri/jobs/{jobId}/cancel, 'sindri jobs cancel'; API spec §3.2): job class, waits for the views, appends 'job.cancelled' and trips the work's token.</summary>
/// <param name="Job">The job.</param>
public sealed record CancelJobRequest(JobId Job) : ICommand<CommandAccepted>;
