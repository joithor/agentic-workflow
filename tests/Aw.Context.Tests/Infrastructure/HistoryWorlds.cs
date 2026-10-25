using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Context.Testing.History;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>Builds <see cref="HistoryScript"/>'s history as a real git repo and as the fake's data.</summary>
internal static class HistoryWorlds
{
    internal static ContextRepo FakeRepo { get; } = new(ContextRepoName.From("aw-split-panes"), "/work/aw-split-panes");

    /// <summary>The real repo: an initial commit, a commit with a body, a branch merged with a merge commit, and a last commit.</summary>
    /// <param name="temp">An empty throwaway repo with the initial files committed.</param>
    /// <returns>The history as the adapter should read it back.</returns>
    internal static HistoryWorld Git(TempGitRepo temp)
    {
        _ = temp.RunAt(HistoryScript.InitialAt, "commit", "-q", "--amend", "-m", HistoryScript.InitialSubject);
        var initial = temp.Run("rev-parse", "HEAD");
        File.WriteAllText(Path.Combine(temp.Path, "b.txt"), "b1\n");
        File.WriteAllText(Path.Combine(temp.Path, HistoryScript.DashPath), "d1\n");
        File.WriteAllText(Path.Combine(temp.Path, "other.txt"), "o2\n");
        _ = temp.Run("add", "-A");
        _ = temp.RunAt(HistoryScript.TouchAt, "commit", "-q", "-m", HistoryScript.TouchSubject, "-m", "Body with a token " + HistoryScript.BodyToken);
        var touch = temp.Run("rev-parse", "HEAD");
        _ = temp.Run("checkout", "-q", "-b", "feat");
        File.WriteAllText(Path.Combine(temp.Path, "a.txt"), "a2\n");
        File.WriteAllText(Path.Combine(temp.Path, "b.txt"), "b2\n");
        _ = temp.RunAt(HistoryScript.SideAt, "commit", "-q", "-am", "side");
        _ = temp.Run("checkout", "-q", "main");
        _ = temp.RunAt(HistoryScript.MergeAt, "merge", "-q", "--no-ff", "feat", "-m", HistoryScript.MergeSubject);
        var merge = temp.Run("rev-parse", "HEAD");
        File.WriteAllText(Path.Combine(temp.Path, "other.txt"), "o3\n");
        _ = temp.RunAt(HistoryScript.EditAt, "commit", "-q", "-am", HistoryScript.EditSubject);
        var edit = temp.Run("rev-parse", "HEAD");
        return Describe(edit, merge, touch, initial);
    }

    /// <summary>The same history as the fake holds it, with made-up shas.</summary>
    /// <returns>The history.</returns>
    internal static HistoryWorld Fake() => Describe(new string('e', 40), new string('a', 40), new string('b', 40), new string('c', 40));

    private static HistoryWorld Describe(string edit, string merge, string touch, string initial) => new(edit,
    [
        new FileChange(edit, HistoryScript.EditAt, HistoryScript.EditSubject, 1, ["other.txt"]),
        new FileChange(merge, HistoryScript.MergeAt, HistoryScript.MergeSubject, 2, ["a.txt", "b.txt"]),
        new FileChange(touch, HistoryScript.TouchAt, HistoryScript.TouchSubject, 1, [HistoryScript.DashPath, "b.txt", "other.txt"]),
        new FileChange(initial, HistoryScript.InitialAt, HistoryScript.InitialSubject, 0, ["a.txt", "other.txt"]),
    ]);

    /// <summary>The files the real repo starts from.</summary>
    internal static IReadOnlyDictionary<string, string> InitialFiles { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["a.txt"] = "a1\n",
        ["other.txt"] = "o1\n",
    };
}
