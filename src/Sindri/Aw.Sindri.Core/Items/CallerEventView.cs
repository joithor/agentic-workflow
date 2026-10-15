using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Items;

/// <summary>
/// What the current caller sees of a ledger row, for the stream and the ledger read (API spec §3.1, §5.2): the
/// <see cref="PublicEvents"/> form (scrubbed and escaped), then the session repo filter. A session sees the rows about its repo's
/// items and the rows about no item, and drops a row whose payload names another repo; a human or service sees every row.
/// The item's repo is read from the ledger, so the answer doesn't depend on the views being current.
/// </summary>
/// <param name="callers">The caller.</param>
/// <param name="repos">Where each item belongs, from the ledger.</param>
/// <param name="scrubber">Masks secrets in every string a payload sends.</param>
/// <param name="json">The package's AwJson options, for the public payloads.</param>
public sealed class CallerEventView(ICallerContextAccessor callers, IItemRepos repos, ITextScrubber scrubber, JsonSerializerOptions json)
{
    /// <summary>The row as the current caller may see it, or null when it may not; an error when the item's repo can't be read.</summary>
    /// <param name="row">The ledger row.</param>
    /// <param name="includeNonPublic">True for a human's ledger read: types outside the catalog's public ones come too.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The event, or null.</returns>
    public async ValueTask<Result<PublicEvent?>> Shown(LedgerEvent row, bool includeNonPublic, CancellationToken cancellationToken)
    {
        var view = PublicEvents.For(row, json, scrubber, includeNonPublic);
        if (view.IsError || view.Value is null || callers.Current is not { } caller)
        {
            return view;
        }

        if (caller.Class == CallerClass.Session && NamesAnotherRepo(view.Value.Payload, caller))
        {
            return (PublicEvent?)null;
        }

        var visible = await EventVisibility.Visible(caller, row, repos, cancellationToken).ConfigureAwait(false);
        return visible.IsError ? visible.Error : visible.Value ? view : (PublicEvent?)null;
    }

    private static bool NamesAnotherRepo(JsonElement payload, CallerContext caller) =>
        payload.ValueKind == JsonValueKind.Object
        && payload.TryGetProperty("repo", out var repo)
        && repo.ValueKind == JsonValueKind.String
        && !string.Equals(repo.GetString(), caller.Repo?.Value, StringComparison.Ordinal);
}
