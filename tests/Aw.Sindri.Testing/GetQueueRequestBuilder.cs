using Aw.Sindri.Features.GetQueue;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetQueueRequest with deterministic defaults.</summary>
public sealed class GetQueueRequestBuilder
{
    private readonly GetQueueRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetQueueRequest Build() => _request;
}
