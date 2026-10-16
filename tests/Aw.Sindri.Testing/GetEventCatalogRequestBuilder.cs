using Aw.Sindri.Features.GetEventCatalog;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetEventCatalogRequest with deterministic defaults.</summary>
public sealed class GetEventCatalogRequestBuilder
{
    private readonly GetEventCatalogRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetEventCatalogRequest Build() => _request;
}
