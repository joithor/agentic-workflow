using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.GetJob;

/// <summary>One job (GET /sindri/jobs/{jobId}; API spec §3.1).</summary>
/// <param name="Job">The job.</param>
public sealed record GetJobRequest(JobId Job) : IQuery<JobView>;
