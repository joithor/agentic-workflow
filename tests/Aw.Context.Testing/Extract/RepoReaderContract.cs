using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Extract;

/// <summary>The repo reader's contract (S-4): the fake and the git reader pass the same tests, and a build reads a commit, never the working tree.</summary>
public abstract class RepoReaderContract
{
    /// <summary>The files every reader's repo holds at its commit.</summary>
    public static IReadOnlyDictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["src/commands/registry.ts"] = "export type CommandId = 'pane.split' | 'pane.close';\n",
        ["README.md"] = "# Split panes\n",
        ["docs/guide.md"] = "Guide\n",
    };

    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>The commit lists its files in ordinal order.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_head_commit_lists_its_files_in_ordinal_order()
    {
        const string test = nameof(The_head_commit_lists_its_files_in_ordinal_order);
        var reader = CreateReader(out var repo, out var commit);

        var head = Expect.Ok(await reader.Head(repo, None), test);
        var listed = Expect.Ok(await reader.ListFiles(repo, head, None), test);

        Expect.That(head == commit, test, $"HEAD was {head}");
        Expect.That(listed.SequenceEqual(Files.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal), test, $"listed {string.Join(", ", listed)}");
    }

    /// <summary>A file reads as it was committed.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_file_reads_as_committed()
    {
        const string test = nameof(A_file_reads_as_committed);
        var reader = CreateReader(out var repo, out var commit);

        var text = Expect.Ok(await reader.Read(repo, commit, "src/commands/registry.ts", None), test);

        Expect.That(text == Files["src/commands/registry.ts"], test, "the text changed");
    }

    /// <summary>A missing repo, commit or path is SND-CONTEXT-048.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_repo_commit_or_path_is_unreadable()
    {
        const string test = nameof(A_missing_repo_commit_or_path_is_unreadable);
        var reader = CreateReader(out var repo, out var commit);
        var missing = new ContextRepo(ContextRepoName.From("nowhere"), Path.Combine(Path.GetTempPath(), "aw-no-such-checkout"));
        const string unknownCommit = "0123456789abcdef0123456789abcdef01234567";

        var results = new[]
        {
            (await reader.Head(missing, None)).IsError ? (await reader.Head(missing, None)).Error.Code : "ok",
            (await reader.ListFiles(repo, unknownCommit, None)).IsError ? (await reader.ListFiles(repo, unknownCommit, None)).Error.Code : "ok",
            (await reader.Read(repo, commit, "src/missing.ts", None)).IsError ? (await reader.Read(repo, commit, "src/missing.ts", None)).Error.Code : "ok",
            (await reader.Read(repo, commit, "../escape", None)).IsError ? (await reader.Read(repo, commit, "../escape", None)).Error.Code : "ok",
        };

        Expect.That(results.All(code => code == "SND-CONTEXT-048"), test, $"codes were {string.Join(", ", results)}");
    }

    /// <summary>A reader with a repo that holds <see cref="Files"/> at one commit.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="commit">Its HEAD commit.</param>
    /// <returns>The reader.</returns>
    protected abstract IRepoReader CreateReader(out ContextRepo repo, out string commit);
}
