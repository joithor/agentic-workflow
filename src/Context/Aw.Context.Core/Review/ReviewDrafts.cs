using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Core.Review;

/// <summary>What the review slices share (S-4): who may review, who is recorded, and the latest draft with its queue. A domain service over the graph store.</summary>
public static class ReviewDrafts
{
    /// <summary>Who a decision is recorded under: the OS user for a human, the engine for a service.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="user">The OS user running the engine.</param>
    /// <returns>The name for the override's 'who'.</returns>
    public static string Who(ContextCaller caller, ICurrentUser user)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(user);
        return caller.Kind == ContextCallerKind.Human ? user.Name : "service:engine";
    }

    /// <summary>Reviewing, promoting and building are for a human or a service: a session is refused (SND-CONTEXT-020); then the settings gate (001).</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="what">What is being done, for the refusal's words.</param>
    /// <returns>Null when allowed, else the error.</returns>
    public static Error? Gate(ContextScope scope, string what)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope.Caller.Kind == ContextCallerKind.Session
            ? ContextErrors.SessionsDisabled.With($"{what} is a human decision: a session can't")
            : MapAccess.Gate(scope);
    }

    /// <summary>
    /// A single review decision (accept, reject, rename, move, undo, repoint, drop): the engine doesn't refuse a session here, since
    /// Sindri's catalog class (desk) alone decides who may call it (X63); only the settings gate (001, and 020 when sessions are off) applies.
    /// The bulk accept is a whole-map path and keeps <see cref="Gate"/>.
    /// </summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="decision">The decision.</param>
    /// <returns>Null when allowed, else the error.</returns>
    public static Error? GateDecision(ContextScope scope, ReviewDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return decision.Verb == ReviewVerb.AcceptDeterministic ? Gate(scope, "the bulk accept") : MapAccess.Gate(scope);
    }

    /// <summary>The newest draft with its overrides, or SND-CONTEXT-044 when there is none.</summary>
    /// <param name="store">The graph store.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The draft, or SND-CONTEXT-044/004.</returns>
    public static async ValueTask<Result<MapSnapshot>> Draft(IGraphStore store, CancellationToken cancellationToken)
    {
        var draft = await store.Snapshot(draft: true, cancellationToken).ConfigureAwait(false);
        if (draft.IsError)
        {
            return draft.Error;
        }

        return draft.Value is { } found
            ? found
            : ContextErrors.NoDraftMap.With("there is no draft map (fix: sindri context map build, sindri context map propose <file>, or name context-rebuild under services: in the profile)");
    }

    /// <summary>
    /// What a reviewer may see of a draft, and what is left to decide in that: <see cref="MapAccess.View"/> with the deny globs only (a review is never cut to a repo),
    /// then the queue built from it. The queue, the undecided count, the bulk accept, an applied review file and the promote check all start here, so a hidden
    /// item is neither listed, nor decided, nor blocking.
    /// </summary>
    /// <param name="draft">The draft snapshot.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <returns>The view and its queue, or SND-CONTEXT-001.</returns>
    public static Result<(MapView View, ReviewQueueView Queue)> Visible(MapSnapshot draft, ContextScope scope)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var view = MapAccess.View(draft.Graph, draft.Overrides, scope, pending: true, callerScope: false);
        return view.IsError ? view.Error : (view.Value, ReviewBacklog.Build(draft.Label, view.Value.Graph, view.Value.Overrides, draft.Version.Repo.Value));
    }

    /// <summary>The latest draft and what is left to decide in it, with labels cleaned the way every answer cleans them.</summary>
    /// <param name="store">The graph store.</param>
    /// <param name="scrubbers">Builds this call's scrubbers.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="what">What is being done, for a refusal's words.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The draft and its queue, or the first refusal.</returns>
    public static async ValueTask<Result<(MapSnapshot Draft, ReviewQueueView Queue)>> Load(
        IGraphStore store, IScrubberFactory scrubbers, ContextScope scope, string what, CancellationToken cancellationToken)
    {
        if (Gate(scope, what) is { } refused)
        {
            return refused;
        }

        var built = SettingsReader.Read(scope.Settings).Bind(s => scrubbers.For(s.ExtraPatterns));
        var draft = built.IsError ? built.Error : await Draft(store, cancellationToken).ConfigureAwait(false);
        if (built.IsError || draft.IsError)
        {
            return built.IsError ? built.Error : draft.Error;
        }

        var text = new MapText(built.Value, FencePolicies.For(scope.Caller));
        var seen = Visible(draft.Value, scope);
        if (seen.IsError)
        {
            return seen.Error;
        }

        var queue = seen.Value.Queue;
        var version = new ReviewVersion(draft.Value.Version.Number, draft.Value.Version.Commit is { Length: > 0 } commit ? commit[..Math.Min(7, commit.Length)] : null);
        return (draft.Value, queue with { Version = version, Entries = [.. queue.Entries.Select(e => e with { Label = text.Label(e.Label, e.Trust) })] });
    }
}
