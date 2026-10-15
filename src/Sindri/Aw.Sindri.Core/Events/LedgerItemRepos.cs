using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// <see cref="IItemRepos"/> over the ledger: one forward pass over the 'item.created' rows, remembered, so each lookup
/// reads only what no earlier lookup read (an item's repo never changes). It reads the ledger itself, never the views.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads an 'item.created' payload.</param>
internal sealed class LedgerItemRepos(ILedgerReader ledger, EventFactory events) : IItemRepos, IDisposable
{
    private const int Page = 500;

    private readonly ConcurrentDictionary<string, RepoName?> _known = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _pass = new(1, 1);
    private long _readTo;

    public async ValueTask<Result<RepoName?>> RepoOf(ItemId item, CancellationToken cancellationToken)
    {
        if (_known.TryGetValue(item.Value, out var known))
        {
            return known;
        }

        await _pass.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (!_known.ContainsKey(item.Value))
            {
                var page = await ledger.ReadAfter(_readTo, Page, cancellationToken).ConfigureAwait(false);
                if (page.IsError)
                {
                    return page.Error;
                }

                foreach (var row in page.Value)
                {
                    Index(row);
                }

                if (page.Value.Count > 0)
                {
                    _readTo = page.Value[^1].Sequence;
                }

                if (page.Value.Count < Page)
                {
                    break;
                }
            }
        }
        finally
        {
            _ = _pass.Release();
        }

        return _known.TryGetValue(item.Value, out var found) ? found : null;
    }

    public void Dispose() => _pass.Dispose();

    private void Index(LedgerEvent row)
    {
        if (row.Item is null || !string.Equals(row.Envelope.Type, EventTypes.ItemCreated, StringComparison.Ordinal))
        {
            return;
        }

        var created = events.Payload<ItemCreated>(row);
        if (created.IsSuccess)
        {
            _known[row.Item] = created.Value.Repo;
        }
    }
}
