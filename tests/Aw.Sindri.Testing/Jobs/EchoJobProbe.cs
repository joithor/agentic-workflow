
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Testing.Jobs;

/// <summary>What the echo work saw, for the contract tests: the actors it ran under and how many of its runs saw a cancellation.</summary>
public sealed class EchoJobProbe
{
    private readonly List<string?> _actors = [];
    private readonly List<JobId?> _jobs = [];
    private int _cancelled;
    private int _runs;
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The actor of the caller current when each run's work started ('human:cli'), in order.</summary>
    public IReadOnlyList<string?> Actors
    {
        get
        {
            lock (_actors)
            {
                return [.. _actors];
            }
        }
    }

    /// <summary>The job id of the job context current when each run's work started (null when none was set), in order.</summary>
    public IReadOnlyList<JobId?> Jobs
    {
        get
        {
            lock (_actors)
            {
                return [.. _jobs];
            }
        }
    }

    /// <summary>How many runs saw their token trip.</summary>
    public int Cancelled => Volatile.Read(ref _cancelled);

    /// <summary>Completes when the first run's work has started.</summary>
    public Task WaitStarted => _started.Task;

    /// <summary>How many runs started.</summary>
    public int Runs => Volatile.Read(ref _runs);

    internal void Started(string? actor, JobId? job)
    {
        lock (_actors)
        {
            _actors.Add(actor);
            _jobs.Add(job);
        }

        _ = Interlocked.Increment(ref _runs);
        _ = _started.TrySetResult();
    }

    internal void SawCancellation() => Interlocked.Increment(ref _cancelled);
}
