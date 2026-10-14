using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Features.ListItems;

namespace Aw.Sindri.Testing;

/// <summary>Builds a ListItemsRequest with deterministic defaults: no filter.</summary>
public sealed class ListItemsRequestBuilder
{
    private ItemState? _state;
    private RepoName? _repo;

    /// <summary>Sets the state filter.</summary>
    /// <param name="value">The state.</param>
    /// <returns>This builder.</returns>
    public ListItemsRequestBuilder WithState(ItemState? value)
    {
        _state = value;
        return this;
    }

    /// <summary>Sets the repo filter.</summary>
    /// <param name="value">The repo.</param>
    /// <returns>This builder.</returns>
    public ListItemsRequestBuilder WithRepo(RepoName? value)
    {
        _repo = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ListItemsRequest Build() => new(_state, _repo);
}
