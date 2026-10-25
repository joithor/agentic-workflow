using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Jobs;

/// <summary>The jobs running in this process, as a start's own checks may read them (a read only: the claim itself is the start service's).</summary>
public interface IRunningJobs
{
    /// <summary>Whether a job of (<paramref name="kind"/>, <paramref name="key"/>) holds a claim, so a check that must not burn anything can ask before it spends.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="key">The key.</param>
    /// <param name="running">The job that holds it, when one does.</param>
    /// <returns>True when a job holds the claim.</returns>
    public bool IsRunning(JobKind kind, JobKey key, out JobId running);
}
