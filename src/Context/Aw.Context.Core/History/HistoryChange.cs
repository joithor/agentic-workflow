using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.History;

/// <summary>A landed change (one first-parent commit) that touched an implementing file of the scope. Subject and branch are untrusted, scrubbed text.</summary>
/// <param name="Key">'commit:' and the 40-hex sha.</param>
/// <param name="Repo">The repo.</param>
/// <param name="Sha">The commit.</param>
/// <param name="At">The committer date.</param>
/// <param name="Subject">The scrubbed one-line subject.</param>
/// <param name="Kind">Merge or other commit.</param>
/// <param name="Branch">The merged branch the subject names (owner stripped), or null.</param>
/// <param name="Pr">The pull request number the subject names, or null.</param>
/// <param name="Files">The searched files the commit touched.</param>
/// <param name="Anchors">The anchors whose scope the files belong to.</param>
/// <param name="Trust">Always untrusted.</param>
public sealed record HistoryChange(
    string Key,
    ContextRepoName Repo,
    string Sha,
    DateTimeOffset At,
    string Subject,
    HistoryChangeKind Kind,
    string? Branch,
    int? Pr,
    IReadOnlyList<string> Files,
    IReadOnlyList<NodeKey> Anchors,
    Results.Trust Trust);
