using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'job.progress' payload (API spec §3.4): one progress line, cleaned when it was written. Public as it is.</summary>
/// <param name="CommandId">The start command's id.</param>
/// <param name="Job">The job.</param>
/// <param name="Round">The line's number in the job, from 1.</param>
/// <param name="Line">The text: scrubbed, escaped, one line, at most 200 characters.</param>
/// <param name="Item">The job's item, copied from its start (the session filter reads it), or null.</param>
/// <param name="Repo">The job's repo, copied from its start (the session filter reads it), or null.</param>
public sealed record JobProgress(CommandId CommandId, JobId Job, int Round, string Line, ItemId? Item = null, RepoName? Repo = null);
