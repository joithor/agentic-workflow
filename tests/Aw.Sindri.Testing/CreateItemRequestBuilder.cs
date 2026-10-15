using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.CreateItem;

namespace Aw.Sindri.Testing;

/// <summary>Builds a CreateItemRequest with deterministic defaults: a title and nothing else.</summary>
public sealed class CreateItemRequestBuilder
{
    private string _title = "Try the queue";
    private RepoName? _repo;
    private string? _body;
    private RecordKey? _link;

    /// <summary>Sets the title.</summary>
    /// <param name="value">The title.</param>
    /// <returns>This builder.</returns>
    public CreateItemRequestBuilder WithTitle(string value)
    {
        _title = value;
        return this;
    }

    /// <summary>Sets the repo.</summary>
    /// <param name="value">The repo.</param>
    /// <returns>This builder.</returns>
    public CreateItemRequestBuilder WithRepo(RepoName? value)
    {
        _repo = value;
        return this;
    }

    /// <summary>Sets the body.</summary>
    /// <param name="value">The body.</param>
    /// <returns>This builder.</returns>
    public CreateItemRequestBuilder WithBody(string? value)
    {
        _body = value;
        return this;
    }

    /// <summary>Sets the tracker link.</summary>
    /// <param name="value">The record key.</param>
    /// <returns>This builder.</returns>
    public CreateItemRequestBuilder WithLink(RecordKey? value)
    {
        _link = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public CreateItemRequest Build() => new(_title, _repo, _body, _link);
}
