using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetSession;

/// <summary>
/// One live session (collector spec CA-2). A ref that is unknown, ended or outside a session caller's repo answers the same SND-SESSION-404, so a session cannot probe other
/// repos. A human also gets the directory, the socket and the state history; a session caller gets none of them.
/// </summary>
internal sealed class GetSessionService(ISessionSnapshot snapshot, IAgentCatalog catalog, IApprovedProfile approvals, ICallerContextAccessor callers, ILaunchViews launches)
    : IService<GetSessionRequest, SessionView>
{
    public async ValueTask<Result<SessionView>> Handle(GetSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("a session needs a caller");
        }

        var now = snapshot.Current;   // one read: the check, the row and the history all come from the same pass
        if (now is not null && SessionRows.Unavailable(now.Problems) is { } unavailable)
        {
            return unavailable;
        }

        var found = now is null ? null : SessionRows.Find(now, request.Ref, caller);
        if (now is null || found is null)
        {
            return SindriErrors.SessionNotFound.With("no live session has that ref (or it is outside this caller's repo)");
        }

        var human = SessionRows.SeesHumanFields(caller);
        var path = await RepoPath(found, cancellationToken).ConfigureAwait(false);
        var row = await Row(found, path, human, cancellationToken).ConfigureAwait(false);
        return human
            ? new SessionView(SessionRows.WithSocket(row, found), found.Cwd, now.History.GetValueOrDefault(found.Ref) ?? [], now.ObservedAt)
            : new SessionView(row, Cwd: null, StateHistory: null, now.ObservedAt);
    }

    private async ValueTask<SessionRow> Row(ObservedSession session, string? path, bool human, CancellationToken cancellationToken)
    {
        var steps = await SessionLaunchSteps.Of(launches, [session], cancellationToken).ConfigureAwait(false);
        return SessionRows.Of(session, catalog.Resolve(session.Ref.Provider), path, human, session.LaunchId is { } launch ? steps.GetValueOrDefault(launch) : null);
    }

    private async ValueTask<string?> RepoPath(ObservedSession session, CancellationToken cancellationToken)
    {
        if (session.Repo is not { } repo)
        {
            return null;
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        return approved.IsSuccess ? approved.Value?.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, repo.Value, StringComparison.Ordinal))?.Path : null;
    }
}
