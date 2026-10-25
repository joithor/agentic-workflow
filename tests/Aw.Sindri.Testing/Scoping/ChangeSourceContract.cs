using System.Text;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Xunit;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>The contract every <see cref="IChangeSource"/> honours, run against the in-memory source and the git one (scoping spec §4, C7).</summary>
public abstract class ChangeSourceContract
{
    private const string Branch = "feat/expiry";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<string, string> Files(params (string Path, string Text)[] files) => files.ToDictionary(static f => f.Path, static f => f.Text, StringComparer.Ordinal);

    /// <summary>A source over a checkout whose <c language="text">main</c> holds <paramref name="baseFiles"/> and whose <c language="text">feat/expiry</c> branch adds <paramref name="commits"/>.</summary>
    /// <param name="baseFiles">The files on main.</param>
    /// <param name="commits">The branch's commits, oldest first.</param>
    /// <param name="checkout">The checkout the source is to be asked about.</param>
    /// <returns>The source.</returns>
    protected abstract IChangeSource Create(IReadOnlyDictionary<string, string> baseFiles, IReadOnlyList<ChangeSeedCommit> commits, out string checkout);

    private IChangeSource Seeded(out string checkout) => Create(
        Files(("README.md", "# App\n")),
        [
            new ChangeSeedCommit("Add the expiry field", Files(("src/links.txt", "expires_at is new\n"))),
            new ChangeSeedCommit("Keep a secret around", Files((".env", "TOKEN=hunter2-secret\n"), ("src/more.txt", "second file\n"))),
        ],
        out checkout);

    /// <summary>A branch reads back with its refs, its commits oldest first and the paths it changes.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_branch_reads_back_with_its_commits_and_changed_paths()
    {
        var source = Seeded(out var checkout);

        var read = await source.Read(checkout, Branch, ChangeRead.Scoping, Token);

        Expect.True(read.IsSuccess);
        Expect.Equal(Branch, read.Value.Ref);
        Expect.True(read.Value.Base.Length > 0 && read.Value.Head.Length > 0 && read.Value.Base != read.Value.Head);
        Expect.Sequence(["Add the expiry field", "Keep a secret around"], read.Value.Commits);
        Expect.Sequence(["src/links.txt", "src/more.txt"], read.Value.Files);
        Expect.True(read.Value.PullRequestBody is null);
        Expect.True(read.Value.Diff.Contains("expires_at is new", StringComparison.Ordinal));
        Expect.True(!read.Value.Truncated);
    }

    /// <summary>A hunk under a denied path is dropped from the diff and its path is listed as denied.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_denied_path_loses_its_hunk_and_is_listed()
    {
        var source = Seeded(out var checkout);

        var read = await source.Read(checkout, Branch, ChangeRead.Scoping, Token);

        Expect.True(read.IsSuccess);
        Expect.Sequence([".env"], read.Value.Denied);
        Expect.True(!read.Value.Diff.Contains("hunter2-secret", StringComparison.Ordinal));
    }

    /// <summary>A diff over the cap is cut to the cap and marked truncated.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_diff_over_the_cap_is_cut_and_marked_truncated()
    {
        var source = Seeded(out var checkout);

        var read = await source.Read(checkout, Branch, new ChangeRead(40), Token);

        Expect.True(read.IsSuccess);
        Expect.True(Encoding.UTF8.GetByteCount(read.Value.Diff) <= 40);
        Expect.True(read.Value.Truncated);
    }

    /// <summary>A reference that names nothing is invalid input.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_reference_that_names_nothing_is_invalid_input()
    {
        var source = Seeded(out var checkout);

        var read = await source.Read(checkout, "feat/absent", ChangeRead.Scoping, Token);

        Expect.Equal(SindriErrors.InvalidInput.Code, read.Error.Code);
    }
}
