using Aw.Sindri.Features.GetSetup;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetSetupRequest with deterministic defaults.</summary>
public sealed class GetSetupRequestBuilder
{
    private readonly GetSetupRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetSetupRequest Build() => _request;
}
