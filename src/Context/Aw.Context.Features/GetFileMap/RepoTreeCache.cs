using System.Collections.Concurrent;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;

namespace Aw.Context.Features.GetFileMap;

/// <summary>
/// The sorted file listing of each repo at its last seen commit: a directory page reads it from here instead of running 'git ls-files' and sorting
/// tens of thousands of paths per call. One instance per host; a new commit or a changed deny list is a new entry.
/// </summary>
internal sealed class RepoTreeCache
{
    private readonly ConcurrentDictionary<string, (string Key, RepoTree Tree)> _byRepo = new(StringComparer.Ordinal);

    public RepoTree? Find(ContextRepo repo, string head) =>
        _byRepo.TryGetValue(repo.Name.Value, out var held) && string.Equals(held.Key, Key(repo, head), StringComparison.Ordinal) ? held.Tree : null;

    public void Keep(ContextRepo repo, string head, RepoTree tree) => _byRepo[repo.Name.Value] = (Key(repo, head), tree);

    private static string Key(ContextRepo repo, string head) => head + "\n" + string.Join('\n', repo.DenyPaths);
}
