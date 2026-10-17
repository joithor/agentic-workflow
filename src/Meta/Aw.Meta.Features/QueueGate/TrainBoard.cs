using Aw.Meta.Core;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.QueueGate;

/// <summary>The train as it stands: each pull request's entry, written to the key-value store whenever one changes so the status can show it.</summary>
/// <param name="values">The store.</param>
/// <param name="main">The main sha the train is pinned to.</param>
internal sealed class TrainBoard(IKeyValueStore values, string main) : IDisposable
{
    private readonly SemaphoreSlim _writing = new(1, 1);
    private readonly List<TrainEntry> _entries = [];

    internal IReadOnlyList<TrainEntry> Entries
    {
        get
        {
            lock (_entries)
            {
                return [.. _entries];
            }
        }
    }

    internal async ValueTask Set(TrainEntry entry, string? ended = null)
    {
        lock (_entries)
        {
            _ = _entries.RemoveAll(e => e.Pr == entry.Pr);
            _entries.Add(entry);
        }

        await Publish(ended).ConfigureAwait(false);
    }

    internal async ValueTask Publish(string? ended = null)
    {
        await _writing.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            // The status is advisory: a store that can't be written must not stop the train.
            _ = await values.Set(KvKeys.Train, string.Join('\n', [.. TrainText.Summary(main, Entries), .. ended is null ? Array.Empty<string>() : [ended]]), CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _ = _writing.Release();
        }
    }

    public void Dispose() => _writing.Dispose();
}
