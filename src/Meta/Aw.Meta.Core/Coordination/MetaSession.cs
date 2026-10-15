using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Tells the meta session from every other session: the hooks act only for the registered one.</summary>
/// <param name="messages">Stores the registered role.</param>
/// <param name="config">The team config.</param>
public sealed class MetaSession(IMessageStore messages, IMetaConfig config)
{
    /// <summary>Resolves a session.</summary>
    /// <param name="session">The session asking.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The team config when <paramref name="session"/> is the registered meta session, null for any other session (or none registered).</returns>
    public async ValueTask<Result<TeamConfig?>> Resolve(SessionId session, CancellationToken cancellationToken)
    {
        var role = await messages.Role(cancellationToken).ConfigureAwait(false);
        if (role.IsError)
        {
            return role.Error;
        }

        if (role.Value is not { } registered || registered.Session != session)
        {
            return Results.Success<TeamConfig?>(value: null);
        }

        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        return team.IsError ? team.Error : Results.Success<TeamConfig?>(team.Value);
    }
}
