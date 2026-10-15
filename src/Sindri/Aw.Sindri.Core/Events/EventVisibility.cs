using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The session repo filter (API spec §3.1, §5.2): a human or service sees every event; a session sees the events of its
/// repo's items and the events about no item, and nothing about another repo's items. An item's repo comes from the ledger
/// (<see cref="IItemRepos"/>), never from the views, so the answer doesn't depend on how far the projector has caught up.
/// </summary>
public static class EventVisibility
{
    /// <summary>True when <paramref name="caller"/> may see <paramref name="row"/>; an error when the item's repo can't be read, so the stream's cursor stays before the row.</summary>
    /// <param name="caller">Who reads.</param>
    /// <param name="row">The ledger row.</param>
    /// <param name="repos">Where each item belongs.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Whether it is visible.</returns>
    public static async ValueTask<Result<bool>> Visible(CallerContext caller, LedgerEvent row, IItemRepos repos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(repos);
        if (caller.Class != CallerClass.Session || row.Item is null)
        {
            return true;
        }

        if (!ItemId.TryFrom(row.Item, out var item))
        {
            return false;   // an item key that isn't an item id belongs to no repo a session can name
        }

        var repo = await repos.RepoOf(item, cancellationToken).ConfigureAwait(false);
        return repo.IsError ? repo.Error : ItemScope.Visible(caller, repo.Value);
    }
}
