using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// The running (kind, key) claims of this process (API P6, plan 1.4 (e)): the start claims inside the gate before it appends, so two starts of the
/// same kind and key cannot both pass; the runner releases the claim when the job ends. It starts empty at every process start, so a dead claim never
/// refuses a start after a restart.
/// </summary>
public sealed class JobRegistry
{
    /// <summary>How many jobs hold a claim.</summary>
    public int Count() => throw new NotSupportedException("red until API P6 unit C");

    /// <summary>Claims (<paramref name="kind"/>, <paramref name="key"/>) for <paramref name="job"/>.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="key">The key.</param>
    /// <param name="job">The job that wants it.</param>
    /// <param name="running">The job that already holds the claim, when it is taken.</param>
    /// <returns>True when claimed; false when another job holds it (then <paramref name="running"/> names it).</returns>
    public bool TryClaim(JobKind kind, JobKey key, JobId job, out JobId running) => throw new NotSupportedException("red until API P6 unit C");

    /// <summary>Releases the claim <paramref name="job"/> holds; a job with none is a no-op.</summary>
    /// <param name="job">The job.</param>
    public void Release(JobId job) => throw new NotSupportedException("red until API P6 unit C");
}
