using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.Api;

/// <summary>The session reads (<see cref="SindriApiClient"/>, continued; collector spec CA-1, CA-2). The live snapshot is not a view: no read-your-writes sequence is carried.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<SessionList>> Sessions(Uri api, SessionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return Get<SessionList>(api, SessionRoutes.List(query), cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<SessionView>> Session(Uri api, string provider, string id, CancellationToken cancellationToken) =>
        Get<SessionView>(api, SessionRoutes.One(provider, id), cancellationToken);
}
