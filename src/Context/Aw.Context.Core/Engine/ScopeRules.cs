using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>
/// Who may read what (spec §3), decided once for every transport: session reads are off until
/// 'context.sessions.enabled' (SND-CONTEXT-020), and a session sees only the repo it names in X-Sindri-Repo. Any other
/// repo, or none, answers as an unknown anchor (SND-CONTEXT-003), never as a hint that it exists.
/// </summary>
public static class ScopeRules
{
    /// <summary>The repos <paramref name="caller"/> may read under <paramref name="settings"/>.</summary>
    /// <param name="settings">The checked settings (its 'repos' narrows the profile's).</param>
    /// <param name="repos">The profile's repos.</param>
    /// <param name="caller">The caller.</param>
    /// <returns>The repos in scope, in profile order; or SND-CONTEXT-001 (context.repos names a repo the profile doesn't have), 020 or 003.</returns>
    public static Result<IReadOnlyList<ContextRepo>> Resolve(ContextSettings settings, IReadOnlyList<ContextRepo> repos, ContextCaller caller)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(caller);
        var unknown = (settings.Repos ?? []).Where(n => !repos.Any(r => r.Name == n)).Select(n => n.Value).ToList();
        if (unknown.Count > 0)
        {
            return ContextErrors.SettingsInvalid.With(
                $"context.repos: '{unknown[0]}' is not a profile repo (repos: {string.Join(", ", repos.Select(r => r.Name.Value))})");
        }

        var mapped = settings.Repos is { } names
            ? repos.Where(r => names.Contains(r.Name)).ToList()
            : [.. repos];
        if (caller.Kind != ContextCallerKind.Session)
        {
            return mapped;
        }

        if (!settings.SessionsEnabled)
        {
            return ContextErrors.SessionsDisabled.With(
                "context reads from agent sessions are off (context.sessions.enabled: false); ask the human to enable them in the profile and approve it");
        }

        var own = mapped.Where(r => r.Name == caller.Repo).ToList();
        return own.Count == 1
            ? own
            : ContextErrors.UnknownAnchor.With("no context is mapped for this session's repo (X-Sindri-Repo)");
    }
}
