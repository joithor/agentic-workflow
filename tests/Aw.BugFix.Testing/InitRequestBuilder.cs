using Aw.BugFix.Features.Init;

namespace Aw.BugFix.Testing;

/// <summary>Builds a InitRequest with deterministic defaults.</summary>
public sealed class InitRequestBuilder
{
    private readonly InitRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public InitRequest Build() => _request;
}
