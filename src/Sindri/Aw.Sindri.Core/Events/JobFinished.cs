using System.Text.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'job.finished' payload (API spec §3.4): the work ended. Its public form, <see cref="JobFinishedPublic"/>, leaves the result out; 'GET /jobs/{id}' serves it, scoped.</summary>
/// <param name="CommandId">The start command's id.</param>
/// <param name="Job">The job.</param>
/// <param name="Outcome">How the work ended.</param>
/// <param name="Summary">How it ended in one cleaned line.</param>
/// <param name="Code">The error code when it failed, otherwise null.</param>
/// <param name="Result">The work's result as JSON (scrubbed, at most 16 KiB), or null.</param>
/// <param name="Item">The job's item, copied from its start, or null.</param>
/// <param name="Repo">The job's repo, copied from its start (the session filter reads it), or null.</param>
public sealed record JobFinished(CommandId CommandId, JobId Job, JobOutcome Outcome, string Summary, string? Code, JsonElement? Result, ItemId? Item = null, RepoName? Repo = null);
