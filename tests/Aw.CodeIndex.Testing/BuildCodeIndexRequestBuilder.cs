using Aw.CodeIndex.Features.BuildCodeIndex;

namespace Aw.CodeIndex.Testing;

/// <summary>Builds a BuildCodeIndexRequest with deterministic defaults.</summary>
public sealed class BuildCodeIndexRequestBuilder
{
    private readonly BuildCodeIndexRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public BuildCodeIndexRequest Build() => _request;
}
