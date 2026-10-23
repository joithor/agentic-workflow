using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>Sindri's HTTP API as the app uses it (<see cref="ISindriApi"/>, continued): the whole map.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/context/map/whole (whole-map spec §2.1): the map as nodes, or as areas when it is over the node budget; one area opened in full; where some nodes sit.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">What is asked.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The map, the API's refusal (SND-CONTEXT-030 for no map, 200 for an unknown area), or SND-UI-002/003.</returns>
    public Task<Result<WholeMapView>> WholeMap(Uri api, WholeMapQuery query, CancellationToken cancellationToken);
}
