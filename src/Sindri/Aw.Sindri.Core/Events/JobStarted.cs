using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'job.started' payload (API spec §3.4): a job-class command was accepted and its work was handed to the runtime. Public as it is.</summary>
/// <param name="CommandId">The start command's id; the job's later events carry it as their causation.</param>
/// <param name="Job">The new job.</param>
/// <param name="Kind">The catalog action that started it.</param>
/// <param name="Key">Its idempotency key within the kind.</param>
/// <param name="Item">The item it is about, or null.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="Service">The service it runs (RunService), or null.</param>
public sealed record JobStarted(CommandId CommandId, JobId Job, JobKind Kind, JobKey Key, ItemId? Item, RepoName? Repo, ServiceName? Service);
