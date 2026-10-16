using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Testing;

/// <summary>
/// The job context for tests: an AsyncLocal like the production accessor, plus <see cref="Entered"/> so a test sees every job whose work ran,
///.
/// </summary>
public sealed class FakeJobContextAccessor : IJobContextAccessor
{
    private readonly AsyncLocal<JobContext?> _current = new();
    private readonly List<JobContext> _entered = [];

    /// <summary>Every context entered so far, in order.</summary>
    public IReadOnlyList<JobContext> Entered
    {
        get
        {
            lock (_entered)
            {
                return [.. _entered];
            }
        }
    }

    public JobContext? Current => _current.Value;

    public IDisposable Enter(JobContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (_entered)
        {
            _entered.Add(context);
        }

        var previous = _current.Value;
        _current.Value = context;
        return new Restore(_current, previous);
    }

    private sealed class Restore(AsyncLocal<JobContext?> current, JobContext? previous) : IDisposable
    {
        public void Dispose() => current.Value = previous;
    }
}
