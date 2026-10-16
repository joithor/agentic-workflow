using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Core.Events;

/// <summary>The public form of 'job.finished' (API spec §3.4): the payload without its result.</summary>
/// <param name="CommandId">The start command's id.</param>
/// <param name="Job">The job.</param>
/// <param name="Outcome">How the work ended.</param>
/// <param name="Summary">How it ended in one cleaned line.</param>
/// <param name="Code">The error code when it failed, otherwise null.</param>
public sealed record JobFinishedPublic(CommandId CommandId, JobId Job, JobOutcome Outcome, string Summary, string? Code);
