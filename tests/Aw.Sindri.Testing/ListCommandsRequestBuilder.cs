using Aw.Sindri.Features.ListCommands;

namespace Aw.Sindri.Testing;

/// <summary>Builds a ListCommandsRequest with deterministic defaults.</summary>
public sealed class ListCommandsRequestBuilder
{
    private readonly ListCommandsRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public ListCommandsRequest Build() => _request;
}
