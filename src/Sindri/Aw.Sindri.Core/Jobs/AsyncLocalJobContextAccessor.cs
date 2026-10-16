namespace Aw.Sindri.Core.Jobs;

/// <summary>The running job held in an AsyncLocal: set for a job's work, it flows into every await below it and is restored on dispose.</summary>
internal sealed class AsyncLocalJobContextAccessor : IJobContextAccessor
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
