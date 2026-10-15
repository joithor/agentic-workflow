using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// Tells the meta session from every other session: the hooks act only for the registered one. /clear gives the meta pane a
/// new session id under the same name: a live session with the registered name is the meta, and its new id is registered.
/// </summary>
/// <param name="messages">Stores the registered role.</param>
/// <param name="config">The team config.</param>
/// <param name="sessions">The live Claude sessions, to re-identify the meta after /clear.</param>
public sealed class MetaSession(IMessageStore messages, IMetaConfig config, ISessionDirectory sessions)
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

        if (role.Value is not { } registered)
        {
            return Results.Success<TeamConfig?>(value: null);
        }

        if (registered.Session != session)
        {
            var renamed = await Reidentify(registered, session, cancellationToken).ConfigureAwait(false);
            if (renamed.IsError || !renamed.Value)
            {
                return renamed.IsError ? renamed.Error : Results.Success<TeamConfig?>(value: null);
            }
        }

        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        return team.IsError ? team.Error : Results.Success<TeamConfig?>(team.Value);
    }

    // The session is the meta when a live session with its id carries the registered name; the new id replaces the old one.
    private async ValueTask<Result<bool>> Reidentify(MetaRole registered, SessionId session, CancellationToken cancellationToken)
    {
        var live = await sessions.Live(cancellationToken).ConfigureAwait(false);
        var same = live.IsSuccess
            && registered.Name.Length > 0
            && live.Value.Any(s => s.Session == session && string.Equals(s.Name, registered.Name, StringComparison.Ordinal));
        if (!same)
        {
            return false;
        }

        var stored = await messages.SetRole(new MetaRole(session, registered.Name), cancellationToken).ConfigureAwait(false);
        return stored.IsError ? stored.Error : true;
    }
}
