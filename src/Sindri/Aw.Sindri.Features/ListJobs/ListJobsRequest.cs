using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ListJobs;

/// <summary>Jobs, newest first (GET /sindri/jobs?state=&amp;kind=, 'sindri jobs'; provisional owner default, API P6 Q1).</summary>
/// <param name="State">Only jobs in this state, or null.</param>
/// <param name="Kind">Only jobs of this kind, or null.</param>
public sealed record ListJobsRequest(JobState? State, JobKind? Kind) : IQuery<JobList>;
