using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>Binds the item routes' raw values (P1 binding rule): an '{itemId}' segment and the '?state=' and '?repo=' filters, each a 400 SND-CLI-003 when malformed.</summary>
internal static class ItemRoute
{
    /// <summary>The id or alias in an '{itemId}' segment.</summary>
    /// <param name="itemId">The segment.</param>
    /// <returns>The reference, or SND-CLI-003.</returns>
    internal static Result<ItemRef> Item(string itemId) =>
        ItemRef.TryParse(itemId, out var item) ? item : SindriErrors.InvalidInput.With("'" + Echo(itemId) + "' isn't an item id (ITM-…) or a tracker key (ENG-142)");

    /// <summary>The '?item=' filter of the event reads (an id or alias), or null when absent.</summary>
    /// <param name="item">The query value.</param>
    /// <returns>The reference, or SND-CLI-003.</returns>
    internal static Result<ItemRef?> ItemFilter(string? item)
    {
        if (string.IsNullOrEmpty(item))
        {
            return (ItemRef?)null;
        }

        var parsed = Item(item);
        return parsed.IsError ? parsed.Error : (ItemRef?)parsed.Value;
    }

    /// <summary>The kebab-case '?state=' filter, or null when absent.</summary>
    /// <param name="state">The query value.</param>
    /// <returns>The state, or SND-CLI-003.</returns>
    internal static Result<ItemState?> State(string? state) => state switch
    {
        null => (ItemState?)null,
        "new" => ItemState.New,
        "queued" => ItemState.Queued,
        "running" => ItemState.Running,
        "needs-decision" => ItemState.NeedsDecision,
        "needs-answer" => ItemState.NeedsAnswer,
        "parked" => ItemState.Parked,
        "taken-over" => ItemState.TakenOver,
        "done" => ItemState.Done,
        "cancelled" => ItemState.Cancelled,
        _ => SindriErrors.InvalidInput.With("'" + Echo(state) + "' isn't an item state"),
    };

    /// <summary>The '?repo=' filter, or null when absent.</summary>
    /// <param name="repo">The query value.</param>
    /// <returns>The repo, or SND-CLI-003.</returns>
    internal static Result<RepoName?> Repo(string? repo)
    {
        if (repo is null)
        {
            return (RepoName?)null;
        }

        return RepoName.TryFrom(repo, out var name) ? name : SindriErrors.InvalidInput.With("'" + Echo(repo) + "' isn't a repo name");
    }

    /// <summary>A refused value as the 400 repeats it: escaped and on one line, so a control sequence in a URL never reaches a terminal.</summary>
    private static string Echo(string value) => SafeText.Line(SafeText.Escape(value), 80);
}
