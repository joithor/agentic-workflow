using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'job.progress' payload (API spec §3.4): one progress line, cleaned when it was written. Public as it is.</summary>
/// <param name="CommandId">The start command's id.</param>
/// <param name="Job">The job.</param>
/// <param name="Round">The line's number in the job, from 1.</param>
/// <param name="Line">The text: scrubbed, escaped, one line, at most 200 characters.</param>
public sealed record JobProgress(CommandId CommandId, JobId Job, int Round, string Line);
