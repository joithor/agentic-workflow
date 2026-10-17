using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.History;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.History;

/// <summary>The contract of <see cref="IFileHistory"/>: the fake and the git adapter pass the same tests, and a read touches commits, never the working tree.</summary>
public abstract class FileHistoryContract
{
    private const string UnknownCommit = "0123456789abcdef0123456789abcdef01234567";
    private static readonly string[] _both = ["a.txt", "b.txt"];

    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>Makes the reader over a repo holding <see cref="HistoryScript"/>'s history.</summary>
    /// <param name="repo">The repo it answers for.</param>
    /// <param name="world">The history it holds.</param>
    /// <returns>The reader.</returns>
    protected abstract IFileHistory Create(out ContextRepo repo, out HistoryWorld world);

    /// <summary>Makes the reader over a shallow clone of that history.</summary>
    /// <param name="repo">The repo it answers for.</param>
    /// <returns>The reader.</returns>
    protected abstract IFileHistory CreateShallow(out ContextRepo repo);

    /// <summary>A repo whose checkout does not exist.</summary>
    /// <param name="repo">The repo.</param>
    /// <returns>The reader.</returns>
    protected abstract IFileHistory CreateMissing(out ContextRepo repo);

    /// <summary>Edits a tracked file in the working tree without committing it (git); the fake has no working tree.</summary>
    /// <param name="repo">The repo.</param>
    protected virtual void DirtyWorkingTree(ContextRepo repo)
    {
    }

    private static IReadOnlyList<string> Shas(FileHistory history) => [.. history.Changes.Select(c => c.Sha)];

    /// <summary>Landed changes come back newest first, a merge once.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Lists_first_parent_changes_touching_the_paths_newest_first()
    {
        const string test = nameof(Lists_first_parent_changes_touching_the_paths_newest_first);
        var reader = Create(out var repo, out var world);

        var history = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, _both, HistoryWindow.Open, 100), None), test);

        string[] expected = [world.Find(HistoryScript.MergeSubject).Sha, world.Find(HistoryScript.TouchSubject).Sha, world.Find(HistoryScript.InitialSubject).Sha];
        Expect.That(Shas(history).SequenceEqual(expected, StringComparer.Ordinal), test, $"shas {string.Join(", ", Shas(history))}");
        Expect.That(history.Head == world.Head && !history.Truncated && !history.Shallow, test, "head, truncated or shallow");
        Expect.That(history.Changes.Select(c => c.At).SequenceEqual([HistoryScript.MergeAt, HistoryScript.TouchAt, HistoryScript.InitialAt]), test, "committer dates");
        Expect.That(history.Changes.Select(c => c.Subject).SequenceEqual([HistoryScript.MergeSubject, HistoryScript.TouchSubject, HistoryScript.InitialSubject], StringComparer.Ordinal), test, "subjects");
    }

    /// <summary>A merge lists the files it brought against its first parent.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_merge_lists_the_files_it_brought()
    {
        const string test = nameof(A_merge_lists_the_files_it_brought);
        var reader = Create(out var repo, out _);

        var history = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, _both, HistoryWindow.Open, 100), None), test);

        var merge = history.Changes[0];
        Expect.That(merge.Parents == 2 && merge.Files.SequenceEqual(_both, StringComparer.Ordinal), test, "parents or files differ: " + string.Join(", ", merge.Files));
        Expect.That(history.Changes.Skip(1).All(c => c.Parents <= 1), test, "a plain commit has at most one parent");
    }

    /// <summary>Only the requested paths are listed, and a commit that touched none is not.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Files_are_only_the_requested_paths()
    {
        const string test = nameof(Files_are_only_the_requested_paths);
        var reader = Create(out var repo, out var world);

        var history = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, ["a.txt"], HistoryWindow.Open, 100), None), test);

        string[] expected = [world.Find(HistoryScript.MergeSubject).Sha, world.Find(HistoryScript.InitialSubject).Sha];
        Expect.That(Shas(history).SequenceEqual(expected, StringComparer.Ordinal), test, $"shas {string.Join(", ", Shas(history))}");
        Expect.That(history.Changes.All(c => c.Files.SequenceEqual(["a.txt"], StringComparer.Ordinal)), test, "a file other than the requested one was listed");
    }

    /// <summary>The window filters by committer date, both ends inclusive.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Before_and_since_filter_by_committer_date_inclusive()
    {
        const string test = nameof(Before_and_since_filter_by_committer_date_inclusive);
        var reader = Create(out var repo, out var world);
        var merge = world.Find(HistoryScript.MergeSubject).Sha;
        var touch = world.Find(HistoryScript.TouchSubject).Sha;

        var both = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, _both, new HistoryWindow(HistoryScript.TouchAt, HistoryScript.MergeAt), 100), None), test);
        var early = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, _both, new HistoryWindow(HistoryScript.TouchAt, HistoryScript.MergeAt.AddSeconds(-1)), 100), None), test);
        var late = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, _both, new HistoryWindow(HistoryScript.TouchAt.AddSeconds(1), Before: null), 100), None), test);

        Expect.That(Shas(both).SequenceEqual([merge, touch], StringComparer.Ordinal), test, $"inclusive ends: {string.Join(", ", Shas(both))}");
        Expect.That(Shas(early).SequenceEqual([touch], StringComparer.Ordinal), test, $"one second before the merge: {string.Join(", ", Shas(early))}");
        Expect.That(Shas(late).SequenceEqual([merge], StringComparer.Ordinal), test, $"one second after the touch: {string.Join(", ", Shas(late))}");
    }

    /// <summary>A bound hides every later commit.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task UpTo_a_commit_hides_later_commits()
    {
        const string test = nameof(UpTo_a_commit_hides_later_commits);
        var reader = Create(out var repo, out var world);
        var touch = world.Find(HistoryScript.TouchSubject).Sha;

        var history = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(touch, _both, HistoryWindow.Open, 100), None), test);

        Expect.That(Shas(history).SequenceEqual([touch, world.Find(HistoryScript.InitialSubject).Sha], StringComparer.Ordinal), test, $"shas {string.Join(", ", Shas(history))}");
        Expect.That(history.Head == world.Head, test, "head is the checkout's HEAD, not the bound");
    }

    /// <summary>A bound the clone doesn't hold is SND-CONTEXT-110, a bound that isn't a sha is SND-CONTEXT-006.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Unknown_up_to_commit_is_110()
    {
        const string test = nameof(Unknown_up_to_commit_is_110);
        var reader = Create(out var repo, out _);

        var unknown = await reader.Changes(repo, new FileHistoryQuery(UnknownCommit, _both, HistoryWindow.Open, 100), None);
        var notSha = await reader.Changes(repo, new FileHistoryQuery("--output=x", _both, HistoryWindow.Open, 100), None);

        Expect.That(unknown.IsError && unknown.Error.Code == ContextErrors.CommitUnknown.Code, test, "an unknown commit wasn't 110");
        Expect.That(notSha.IsError && notSha.Error.Code == ContextErrors.QueryInvalid.Code, test, "a non-sha bound wasn't 006");
    }

    /// <summary>An uncommitted edit changes nothing: history comes from commits.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Never_reads_the_working_tree()
    {
        const string test = nameof(Never_reads_the_working_tree);
        var reader = Create(out var repo, out _);
        var before = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, _both, HistoryWindow.Open, 100), None), test);

        DirtyWorkingTree(repo);
        var after = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, _both, HistoryWindow.Open, 100), None), test);

        Expect.That(Shas(before).SequenceEqual(Shas(after), StringComparer.Ordinal) && after.Changes.Count == 3, test, "the dirty working tree changed the answer");
    }

    /// <summary>More changes than the cap come back as the newest ones with truncated set.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Caps_and_says_truncated()
    {
        const string test = nameof(Caps_and_says_truncated);
        var reader = Create(out var repo, out var world);

        var capped = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, _both, HistoryWindow.Open, 2), None), test);
        var exact = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, _both, HistoryWindow.Open, 3), None), test);

        Expect.That(Shas(capped).SequenceEqual([world.Find(HistoryScript.MergeSubject).Sha, world.Find(HistoryScript.TouchSubject).Sha], StringComparer.Ordinal) && capped.Truncated, test, "two newest and truncated");
        Expect.That(exact.Changes.Count == 3 && !exact.Truncated, test, "exactly the cap is not truncated");
    }

    /// <summary>A name that starts with a dash is a path, and a glob character is matched literally.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_path_starting_with_a_dash_is_a_path()
    {
        const string test = nameof(A_path_starting_with_a_dash_is_a_path);
        var reader = Create(out var repo, out var world);

        var dash = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, [HistoryScript.DashPath], HistoryWindow.Open, 100), None), test);
        var glob = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, ["*.txt"], HistoryWindow.Open, 100), None), test);

        Expect.That(Shas(dash).SequenceEqual([world.Find(HistoryScript.TouchSubject).Sha], StringComparer.Ordinal), test, $"dash path: {string.Join(", ", Shas(dash))}");
        Expect.That(glob.Changes.Count == 0, test, "a glob matched files");
    }

    /// <summary>A control character in a path is refused with SND-CONTEXT-006, and no paths give no changes.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_control_character_in_a_path_is_refused()
    {
        const string test = nameof(A_control_character_in_a_path_is_refused);
        var reader = Create(out var repo, out _);

        var newline = await reader.Changes(repo, new FileHistoryQuery(UpTo: null, ["a.txt\n--output=x"], HistoryWindow.Open, 100), None);
        var empty = await reader.Changes(repo, new FileHistoryQuery(UpTo: null, [string.Empty], HistoryWindow.Open, 100), None);
        var none = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, [], HistoryWindow.Open, 100), None), test);

        Expect.That(newline.IsError && newline.Error.Code == ContextErrors.QueryInvalid.Code, test, "a newline in a path wasn't 006");
        Expect.That(empty.IsError && empty.Error.Code == ContextErrors.QueryInvalid.Code, test, "an empty path wasn't 006");
        Expect.That(none.Changes.Count == 0 && !none.Truncated, test, "no paths listed changes");
    }

    /// <summary>A message body never reaches the answer: only a subject line is read.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Only_the_subject_is_read()
    {
        const string test = nameof(Only_the_subject_is_read);
        var reader = Create(out var repo, out _);

        var history = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, [HistoryScript.DashPath, "b.txt"], HistoryWindow.Open, 100), None), test);

        var everything = string.Join('\n', history.Changes.SelectMany(c => c.Files.Append(c.Subject).Append(c.Sha)));
        Expect.That(history.Changes.Any(c => c.Subject == HistoryScript.TouchSubject), test, "the commit with a body wasn't found");
        Expect.That(!everything.Contains(HistoryScript.BodyToken, StringComparison.Ordinal), test, "a body token reached the answer");
    }

    /// <summary>A shallow clone is reported so the caller can say history may be missing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_shallow_clone_is_reported()
    {
        const string test = nameof(A_shallow_clone_is_reported);
        var reader = CreateShallow(out var repo);

        var history = Expect.Ok(await reader.Changes(repo, new FileHistoryQuery(UpTo: null, _both, HistoryWindow.Open, 100), None), test);

        Expect.That(history.Shallow && history.Changes.Count >= 1, test, "a shallow clone wasn't reported");
    }

    /// <summary>A checkout git can't read is SND-CONTEXT-048.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unreadable_checkout_is_048()
    {
        const string test = nameof(An_unreadable_checkout_is_048);
        var reader = CreateMissing(out var repo);

        var history = await reader.Changes(repo, new FileHistoryQuery(UpTo: null, _both, HistoryWindow.Open, 100), None);

        Expect.That(history.IsError && history.Error.Code == ContextErrors.ExtractionUnreadable.Code, test, "a missing checkout wasn't 048");
    }
}
