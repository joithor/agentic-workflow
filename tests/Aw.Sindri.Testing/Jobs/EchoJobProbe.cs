
namespace Aw.Sindri.Testing.Jobs;

/// <summary>What the echo work saw, for the contract tests: the actors it ran under and how many of its runs saw a cancellation.</summary>
public sealed class EchoJobProbe
{
    private readonly List<string?> _actors = [];
    private int _cancelled;
    private int _runs;

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

    /// <summary>How many runs saw their token trip.</summary>
    public int Cancelled => Volatile.Read(ref _cancelled);

    /// <summary>How many runs started.</summary>
    public int Runs => Volatile.Read(ref _runs);

    internal void Started(string? actor)
    {
        lock (_actors)
        {
            _actors.Add(actor);
        }

        _ = Interlocked.Increment(ref _runs);
    }

    internal void SawCancellation() => Interlocked.Increment(ref _cancelled);
}
