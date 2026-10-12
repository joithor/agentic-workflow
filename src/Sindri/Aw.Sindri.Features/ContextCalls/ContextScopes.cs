using Aw.Context.Core.Engine;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The one mapping from Sindri to the context engine's scope (context spec §4): the approved profile's 'context:'
/// section and repos, and the current caller as the engine's caller (class, X-Sindri-Repo, transport from the actor:
/// 'human:cli' is cli, 'session:mcp' is mcp, other HTTP callers http, services service). Every surface (HTTP, CLI, MCP,
/// the scheduled refresh) goes through it, and the engine decides the session gate and repo scope (SND-CONTEXT-020,
/// 003). It reads approvals only (<see cref="IApprovals.Latest"/>); nothing here writes.
/// </summary>
/// <param name="approvals">The approved profiles.</param>
/// <param name="callers">The current caller.</param>
internal sealed class ContextScopes(IApprovals approvals, ICallerContextAccessor callers)
{
    /// <summary>The scope of the call running now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scope, or SND-PROFILE-005 when no profile is approved (or the approvals' own error).</returns>
    internal async ValueTask<Result<ContextScope>> Current(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileNotApproved.With("no profile is approved, so no repo is mapped (fix: sindri profile approve)");
        }

        var repos = (loaded.Profile.Repos ?? []).Where(r => ContextRepoName.TryFrom(r.Name, out _)).Select(r => new ContextRepo(ContextRepoName.From(r.Name), r.Path)).ToList();
        return new ContextScope(loaded.Profile.Context, repos, Caller(callers.Current));
    }

    /// <summary>The engine's caller for a Sindri caller; no caller at all is an in-process service.</summary>
    /// <param name="caller">The Sindri caller, or null.</param>
    /// <returns>The engine's caller.</returns>
    internal static ContextCaller Caller(CallerContext? caller) => caller switch
    {
        { Class: CallerClass.Human } => new ContextCaller(ContextCallerKind.Human, Repo: null, Client(caller.Actor)),
        { Class: CallerClass.Session } => new ContextCaller(ContextCallerKind.Session, caller.Repo is { } repo && ContextRepoName.TryFrom(repo.Value, out var name) ? name : null, Client(caller.Actor)),
        _ => new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service),
    };

    private static ContextClient Client(string actor) =>
        actor.EndsWith(":cli", StringComparison.Ordinal) ? ContextClient.Cli
        : actor.EndsWith(":mcp", StringComparison.Ordinal) ? ContextClient.Mcp
        : ContextClient.Http;
}
