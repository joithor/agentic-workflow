using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.Api;

/// <summary>Sindri's HTTP API as the app uses it (<see cref="ISindriApi"/>, continued): an item's frozen scope.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/items/{id}/scope: the starting points and map version the item's scoping was approved at.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The scope (null inside when none is frozen), SND-ITEM-404, or SND-UI-002/003.</returns>
    public Task<Result<ItemScopeView>> ItemScope(Uri api, string itemId, CancellationToken cancellationToken);
}
