using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>The app's reads of the API (<see cref="ISindriApi"/>, continued): the product's concepts, the first layer of the context map.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/context/concepts: the product's core concepts with their sizes. A server without the route answers 404, which the page reads as "no concepts".</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="repo">The repo to read; empty for the profile's only repo.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The concepts, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ContextConceptsView>> ContextConcepts(Uri api, string repo, CancellationToken cancellationToken);
}
