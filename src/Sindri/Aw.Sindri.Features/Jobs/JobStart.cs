using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.Jobs;

/// <summary>What the start hands the runner once 'job.started' is appended and the (kind, key) claim is held (API P6).</summary>
/// <param name="Job">The new job.</param>
/// <param name="Kind">Its kind (the catalog action).</param>
/// <param name="Key">Its key within the kind.</param>
/// <param name="Definition">The registered definition.</param>
/// <param name="Request">The request record.</param>
/// <param name="Caller">The starting caller; the work re-enters it (RunService re-enters 'service:&lt;name&gt;' instead).</param>
/// <param name="Grant">The lease grant every write is fenced by.</param>
/// <param name="Service">The service the job runs, or null.</param>
/// <param name="Cause">The envelope id of the 'job.started' event, which the job's later events carry as their causation (the engine bridge routes by it), or null.</param>
/// <param name="Repo">The job's repo as 'job.started' recorded it; the job's later events carry it so a session sees them only in that repo, or null.</param>
public sealed record JobStart(JobId Job, JobKind Kind, JobKey Key, JobDefinition Definition, object Request, CallerContext Caller, LeaseGrant Grant, ServiceName? Service, EventId? Cause = null, RepoName? Repo = null);
