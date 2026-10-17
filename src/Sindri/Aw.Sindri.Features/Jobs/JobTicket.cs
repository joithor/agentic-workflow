using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.Jobs;

/// <summary>Who a job is: its id, its kind and its key within the kind.</summary>
/// <param name="Job">The job.</param>
/// <param name="Kind">The catalog action that started it.</param>
/// <param name="Key">Its idempotency key within the kind.</param>
internal readonly record struct JobTicket(JobId Job, JobKind Kind, JobKey Key);
