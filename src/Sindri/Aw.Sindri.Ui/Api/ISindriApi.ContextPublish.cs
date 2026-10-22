using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>Sindri's HTTP API as the app uses it (<see cref="ISindriApi"/>, continued): the map's publish state.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/context/map/publish: whether the current map is held for the code index, or how it was published (the Health page's Context map card).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The publish state, the API's refusal (SND-CONTEXT-030 while there is no map), or SND-UI-002/003.</returns>
    public Task<Result<MapPublishView>> MapPublish(Uri api, CancellationToken cancellationToken);
}
