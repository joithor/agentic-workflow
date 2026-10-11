using Aw.Sindri.Features.GetStatus;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetStatusRequest with deterministic defaults.</summary>
public sealed class GetStatusRequestBuilder
{
    private readonly GetStatusRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetStatusRequest Build() => _request;
}
