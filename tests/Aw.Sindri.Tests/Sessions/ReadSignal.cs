namespace Aw.Sindri.Tests.Sessions;

/// <summary>A counter a test can await: <see cref="Reached"/> completes once the count has got to a number, so a test waits on what happened rather than on time.</summary>
internal sealed class ReadSignal
{
    private readonly Lock _gate = new();
    private readonly List<(int At, TaskCompletionSource Done)> _waiting = [];
    private int _count;

    public int Count { get { lock (_gate) { return _count; } } }

    public void Increment()
    {
        lock (_gate)
        {
            _count++;
            foreach (var (at, done) in _waiting.Where(w => w.At <= _count).ToList())
            {
                _ = done.TrySetResult();
                _ = _waiting.RemoveAll(w => w.Done == done);
            }
        }
    }

    public Task Reached(int count)
    {
        lock (_gate)
        {
            if (_count >= count)
            {
                return Task.CompletedTask;
            }

            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiting.Add((count, done));
            return done.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }
    }
}
