using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Items;

/// <summary>
/// The session repo scope of the instance routes (API spec §3.8): a human or service reads every instance; a session reads the
/// instances on its own repo's items. The item's repo comes from the ledger (<see cref="IItemRepos"/>), never from the views.
/// </summary>
public static class InstanceScope
{
    /// <summary>Null when <paramref name="caller"/> may read the item's instances; SND-ITEM-404 for a session of another repo (or an instance on no item); the read's error when the repo can't be read.</summary>
    /// <param name="caller">Who reads.</param>
    /// <param name="item">The item the instance is on.</param>
    /// <param name="repos">Where each item belongs.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The refusal, or null.</returns>
    public static async ValueTask<Error?> Refusal(CallerContext caller, ItemKey? item, IItemRepos repos, CancellationToken cancellationToken)
    {
        if (caller.Class != CallerClass.Session)
        {
            return null;
        }

        if (item is null || !ItemId.TryFrom(item.Value.Value, out var id))
        {
            return SindriErrors.ItemNotFound.With("no such instance");
        }

        var repo = await repos.RepoOf(id, cancellationToken).ConfigureAwait(false);
        if (repo.IsError)
        {
            return repo.Error;
        }

        return ItemScope.Visible(caller, repo.Value) ? null : SindriErrors.ItemNotFound.With("no such instance");
    }
}
