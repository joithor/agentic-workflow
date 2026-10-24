using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>Who a whole-map or search answer is for: a session its own repo, a human the repo they named (a profile with several cuts the graph to it) or the profile's only repo.</summary>
public static class RepoChoices
{
    /// <summary>Who an answer is for.</summary>
    /// <param name="settings">The checked context settings.</param>
    /// <param name="scope">The profile's repos and the caller.</param>
    /// <param name="asked">The repo a human named, or null.</param>
    /// <returns>The choice, or SND-CONTEXT-045 for a repo the caller can't read, or the scope's own refusal (001, 020).</returns>
    public static Result<RepoChoice> Addressed(ContextSettings settings, ContextScope scope, string? asked)
    {
        var resolved = ScopeRules.Resolve(settings, scope.Repos, scope.Caller);
        if (resolved.IsError && resolved.Error.Code != ContextErrors.UnknownAnchor.Code)
        {
            return resolved.Error;
        }

        var repos = resolved.IsError ? [] : resolved.Value;
        if (asked is null)
        {
            return new RepoChoice(repos.Count == 1 ? repos[0].Name.Value : scope.Caller.Repo?.Value, Cut: null);
        }

        var named = repos.FirstOrDefault(r => string.Equals(r.Name.Value, asked, StringComparison.Ordinal));
        return named is null
            ? ContextErrors.RepoOutsideScope.With("that repo isn't one this caller can read (fix: name a repo of the approved profile; a session names its own X-Sindri-Repo)")
            : new RepoChoice(named.Name.Value, repos.Count > 1 && scope.Caller.Kind != ContextCallerKind.Session ? named.Name : null);
    }
}
