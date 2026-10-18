using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Code;

/// <summary>One file search (S-CL): the repo and commit, the terms, the paths to stay within and the cap.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Commit">A commit sha, full or abbreviated.</param>
/// <param name="Terms">The terms: literal text, at most 200 characters, no control character; a term may start with '-'.</param>
/// <param name="Within">When not null, only these paths are searched (literal paths, not globs).</param>
/// <param name="Max">The most paths to return.</param>
public sealed record CodeSearchQuery(ContextRepo Repo, string Commit, IReadOnlyList<string> Terms, IReadOnlyList<string>? Within, int Max)
{
    /// <summary>True when <see cref="Commit"/> is a full sha that <see cref="ICodeSearch.Resolve"/> returned, so the search does not resolve it again for each call.</summary>
    public bool Resolved { get; init; }

    /// <summary>The first term that is empty, over <see cref="CodeRules.MaxTermLength"/> characters or has a control character.</summary>
    /// <returns>SND-CONTEXT-006, or null when every term is searchable. The message never repeats the term.</returns>
    public Error? TermsProblem() =>
        Terms.Any(t => string.IsNullOrEmpty(t) || t.Length > CodeRules.MaxTermLength || t.Any(char.IsControl))
            ? ContextErrors.QueryInvalid.With($"a search term is empty, over {CodeRules.MaxTermLength.ToString(System.Globalization.CultureInfo.InvariantCulture)} characters or has a control character")
            : null;
}
