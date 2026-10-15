using Aw.Sindri.Features.ListServices;

namespace Aw.Sindri.Testing;

/// <summary>Builds a ListServicesRequest with deterministic defaults.</summary>
public sealed class ListServicesRequestBuilder
{
    private readonly ListServicesRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ListServicesRequest Build() => _request;
}
