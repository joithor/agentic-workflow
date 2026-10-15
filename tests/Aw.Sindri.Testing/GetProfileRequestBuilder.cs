using Aw.Sindri.Features.GetProfile;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetProfileRequest with deterministic defaults.</summary>
public sealed class GetProfileRequestBuilder
{
    private readonly GetProfileRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetProfileRequest Build() => _request;
}
