using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;

namespace Aw.Sindri.Core.Items;

/// <summary>
/// The session repo scope of the instance routes (API spec §3.8): a human or service reads every instance; a session reads the
/// instances on its own repo's items. The item's repo comes from the ledger (<see cref="IItemRepos"/>), never from the views.
/// </summary>
public static class InstanceScope
{
    /// <summary>The one answer for an instance the caller can't read, whether it doesn't exist or is on another repo's item: SND-ITEM-404, so an id's answer never says it exists elsewhere.</summary>
    /// <param name="reference">What the caller asked for (the instance id or address).</param>
    /// <returns>The error.</returns>
    public static Error NotFound(string reference) => SindriErrors.ItemNotFound.With($"No workflow instance {reference} in this repo.");

    /// <summary>The instance read's error as the caller sees it: no such instance (SND-WF-001) becomes <see cref="NotFound"/>; any other error is passed on.</summary>
    /// <param name="error">The error the directory or the runtime answered.</param>
    /// <param name="reference">What the caller asked for.</param>
    /// <returns>The error to answer.</returns>
    public static Error Unknown(Error error, string reference)
    {
        ArgumentNullException.ThrowIfNull(error);
        return string.Equals(error.Code, WorkflowErrors.InstanceNotFound.Code, StringComparison.Ordinal) ? NotFound(reference) : error;
    }

    /// <summary>Null when <paramref name="caller"/> may read the item's instances; SND-ITEM-404 for a session of another repo (or an instance on no item); the read's error when the repo can't be read.</summary>
    /// <param name="caller">Who reads.</param>
    /// <param name="item">The item the instance is on.</param>
    /// <param name="reference">What the caller asked for, named in the refusal.</param>
    /// <param name="repos">Where each item belongs.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The refusal, or null.</returns>
    public static async ValueTask<Error?> Refusal(CallerContext caller, ItemKey? item, string reference, IItemRepos repos, CancellationToken cancellationToken)
    {
        if (caller.Class != CallerClass.Session)
        {
            return null;
        }

        if (item is null || !ItemId.TryFrom(item.Value.Value, out var id))
        {
            return NotFound(reference);
        }

        var repo = await repos.RepoOf(id, cancellationToken).ConfigureAwait(false);
        if (repo.IsError)
        {
            return repo.Error;
        }

        return ItemScope.Visible(caller, repo.Value) ? null : NotFound(reference);
    }

    /// <summary>
    /// The rows <paramref name="caller"/> may read: all of them for a human or a service, a session's own repo's for a session. An item is read
    /// once; the item comes from the address, the repo from the ledger.
    /// </summary>
    /// <param name="caller">Who reads.</param>
    /// <param name="rows">The listed instances.</param>
    /// <param name="repos">Where each item belongs.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The visible rows, or the error when a repo can't be read.</returns>
    public static async ValueTask<Result<IReadOnlyList<InstanceRow>>> Visible(CallerContext caller, IReadOnlyList<InstanceRow> rows, IItemRepos repos, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(rows);
        if (caller.Class != CallerClass.Session)
        {
            return Results.Success(rows);
        }

        var seen = new Dictionary<string, bool>(StringComparer.Ordinal);
        var kept = new List<InstanceRow>();
        foreach (var row in rows)
        {
            var item = row.Address.AsSpan(0, Math.Max(0, row.Address.IndexOf('/', StringComparison.Ordinal))).ToString();
            if (!seen.TryGetValue(item, out var visible))
            {
                var refusal = ItemKey.TryFrom(item, out var key)
                    ? await Refusal(caller, key, row.Address, repos, cancellationToken).ConfigureAwait(false)
                    : NotFound(row.Address);
                if (refusal is { } error && !string.Equals(error.Code, SindriErrors.ItemNotFound.Code, StringComparison.Ordinal))
                {
                    return error;
                }

                visible = refusal is null;
                seen[item] = visible;
            }

            if (visible)
            {
                kept.Add(row);
            }
        }

        return kept;
    }
}
