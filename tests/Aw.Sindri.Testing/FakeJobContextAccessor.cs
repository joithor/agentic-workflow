using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Testing;

/// <summary>The job context for tests: an AsyncLocal like the production accessor (which is internal to Core).</summary>
public sealed class FakeJobContextAccessor : IJobContextAccessor
{
    private readonly AsyncLocal<JobContext?> _current = new();

    public JobContext? Current => _current.Value;

    public IDisposable Enter(JobContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = _current.Value;
        _current.Value = context;
        return new Restore(_current, previous);
    }

    private sealed class Restore(AsyncLocal<JobContext?> current, JobContext? previous) : IDisposable
    {
        public void Dispose() => current.Value = previous;
    }
}
