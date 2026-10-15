using Aw.Sindri.Features.RebuildViews;

namespace Aw.Sindri.Testing;

/// <summary>Builds a RebuildViewsRequest with deterministic defaults.</summary>
public sealed class RebuildViewsRequestBuilder
{
    private readonly RebuildViewsRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public RebuildViewsRequest Build() => _request;
}
