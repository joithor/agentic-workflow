using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>Whose repo a map read is for: the one rule the whole map and the concepts reads share.</summary>
public static class RepoAddressing
{
    // Who the answer is for: a session its own repo, a human the repo they named (a profile with several cuts the graph to it) or the profile's only repo.
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
