using Aw.Readiness.App.Features.CheckRepo;

namespace Aw.Readiness.Testing;

/// <summary>Builds a CheckRepoRequest with deterministic defaults.</summary>
internal sealed class CheckRepoRequestBuilder
{
    private readonly CheckRepoRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public CheckRepoRequest Build() => _request;
}
