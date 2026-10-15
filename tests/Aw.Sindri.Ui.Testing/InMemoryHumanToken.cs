using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory human token: whatever text the test put there (trimmed as the file's is), or none.</summary>
/// <param name="contents">The token file's text at first, or null for none.</param>
internal sealed class InMemoryHumanToken(string? contents = null) : IHumanTokenSource
{
    private string? _contents = contents;
    private Exception? _fail;

    public string Location => "memory:auth/human.token";

    /// <summary>How many reads were made.</summary>
    public int Reads => Volatile.Read(ref _reads);

    private int _reads;

    /// <summary>Writes the token file's text, as serve does at each start; null removes it, as a clean stop does.</summary>
    /// <param name="contents">The text, or null.</param>
    public void Write(string? contents) => Volatile.Write(ref _contents, contents);

    /// <summary>Queues what the next reads return, one each, in order; after the queue the last one stays.</summary>
    /// <param name="reads">The token file's text at each read, null for none.</param>
    public void Then(params string?[] reads)
    {
        lock (_queue)
        {
            foreach (var read in reads)
            {
                _queue.Enqueue(read);
            }
        }
    }

    private readonly Queue<string?> _queue = new();

    /// <summary>Makes every read throw, as a token file the platform can't read does (no permission, a directory).</summary>
    /// <param name="error">The exception, or null to read again.</param>
    public void FailReads(Exception? error) => Volatile.Write(ref _fail, error);

    public ValueTask<string?> Read(CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _reads);
        if (Volatile.Read(ref _fail) is { } error)
        {
            throw error;
        }

        lock (_queue)
        {
            if (_queue.TryDequeue(out var next))
            {
                Volatile.Write(ref _contents, next);
            }
        }

        var text = Volatile.Read(ref _contents)?.Trim();
        return ValueTask.FromResult(string.IsNullOrEmpty(text) ? null : text);
    }
}
