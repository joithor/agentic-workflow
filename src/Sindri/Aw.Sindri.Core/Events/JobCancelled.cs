using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'job.cancelled' payload (API spec §3.4): 'CancelJob' ended a running job. Public as it is.</summary>
/// <param name="CommandId">The 'CancelJob' command's id.</param>
/// <param name="Job">The job.</param>
/// <param name="By">Who cancelled, as the envelope's actor reads.</param>
/// <param name="Item">The job's item, copied from its start, or null.</param>
/// <param name="Repo">The job's repo, copied from its start (the session filter reads it), or null.</param>
public sealed record JobCancelled(CommandId CommandId, JobId Job, string By, ItemId? Item = null, RepoName? Repo = null);
