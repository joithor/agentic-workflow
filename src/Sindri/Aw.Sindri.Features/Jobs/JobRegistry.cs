using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// The running (kind, key) claims of this process (API P6, plan 1.4 (e)): the start claims inside the gate before it appends, so two starts of the
/// same kind and key cannot both pass; the runner releases the claim when the job ends. It starts empty at every process start, so a dead claim never
/// refuses a start after a restart.
/// </summary>
public sealed class JobRegistry : IRunningJobs
{
    private readonly Lock _gate = new();
    private readonly Dictionary<(string Kind, string Key), JobId> _byKey = [];
    private readonly Dictionary<string, (string Kind, string Key)> _byJob = new(StringComparer.Ordinal);

    /// <summary>How many jobs hold a claim.</summary>
    /// <returns>The count.</returns>
    public int Count()
    {
        lock (_gate)
        {
            return _byJob.Count;
        }
    }

    /// <summary>Whether <paramref name="job"/> holds a claim: this process is running it.</summary>
    /// <param name="job">The job.</param>
    /// <returns>True when it holds one.</returns>
    public bool Holds(JobId job)
    {
        lock (_gate)
        {
            return _byJob.ContainsKey(job.Value);
        }
    }

    /// <summary>Claims (<paramref name="kind"/>, <paramref name="key"/>) for <paramref name="job"/>.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="key">The key.</param>
    /// <param name="job">The job that wants it.</param>
    /// <param name="running">The job that already holds the claim, when it is taken.</param>
    /// <returns>True when claimed; false when another job holds it (then <paramref name="running"/> names it; when claimed it is <paramref name="job"/> itself).</returns>
    public bool TryClaim(JobKind kind, JobKey key, JobId job, out JobId running)
    {
        var slot = (kind.Value, key.Value);
        lock (_gate)
        {
            if (_byKey.TryGetValue(slot, out running))
            {
                return false;
            }

            _byKey[slot] = job;
            _byJob[job.Value] = slot;
            running = job;
            return true;
        }
    }

    /// <inheritdoc/>
    public bool IsRunning(JobKind kind, JobKey key, out JobId running)
    {
        lock (_gate)
        {
            return _byKey.TryGetValue((kind.Value, key.Value), out running);
        }
    }

    /// <summary>Releases the claim <paramref name="job"/> holds; a job with none is a no-op.</summary>
    /// <param name="job">The job.</param>
    public void Release(JobId job)
    {
        lock (_gate)
        {
            if (_byJob.Remove(job.Value, out var slot))
            {
                _ = _byKey.Remove(slot);
            }
        }
    }
}
