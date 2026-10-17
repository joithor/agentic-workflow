using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Jobs;

/// <summary>What the code running a job can see of it (API P6): its id and kind, and where to report a progress line.</summary>
/// <param name="Job">The running job.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Progress">Takes one progress line; the runtime cleans, bounds and records it, so the caller never blocks on the ledger.</param>
public sealed record JobContext(JobId Job, JobKind Kind, IProgress<string> Progress);
