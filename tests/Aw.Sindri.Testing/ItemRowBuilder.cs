using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Testing;

/// <summary>Builds an items-view row with deterministic defaults: A.FirstItemId, new, version 1, created by a human at the epoch.</summary>
public sealed class ItemRowBuilder
{
    private ItemRow _row = new(A.FirstItemId, "Try the queue", Repo: null, ItemState.New, Step: null, Version: 1, "human:desk",
        KnownInstants.Epoch, KnownInstants.Epoch, KnownInstants.Epoch, Link: null, Alias: null, Body: null, WaitingOn: null,
        Instance: null, Owner: null, Sequence: 1);

    /// <summary>Sets the id.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public ItemRowBuilder WithId(ItemId value)
    {
        _row = _row with { Id = value };
        return this;
    }

    /// <summary>Sets the repo.</summary>
    /// <param name="value">The repo, or null.</param>
    /// <returns>This builder.</returns>
    public ItemRowBuilder InRepo(RepoName? value)
    {
        _row = _row with { Repo = value };
        return this;
    }

    /// <summary>Sets the state, entered at <paramref name="since"/>.</summary>
    /// <param name="value">The state.</param>
    /// <param name="since">When it entered it.</param>
    /// <returns>This builder.</returns>
    public ItemRowBuilder InState(ItemState value, DateTimeOffset since)
    {
        _row = _row with { State = value, Since = since, UpdatedAt = since };
        return this;
    }

    /// <summary>Sets the version.</summary>
    /// <param name="value">The version.</param>
    /// <returns>This builder.</returns>
    public ItemRowBuilder AtVersion(long value)
    {
        _row = _row with { Version = value };
        return this;
    }

    /// <summary>Builds the row.</summary>
    /// <returns>The row.</returns>
    public ItemRow Build() => _row;
}
