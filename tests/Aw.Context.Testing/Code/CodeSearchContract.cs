using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Code;

/// <summary>The code search's contract (S-CL): the fake and the git search pass the same tests, and a search reads a commit, never the working tree.</summary>
public abstract class CodeSearchContract
{
    /// <summary>The files every search's repo holds at its commit.</summary>
    public static IReadOnlyDictionary<string, string> Committed { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["src/sharing/links.ts"] = "export const SharedLink = 1;\n",
        ["migrations/002.sql"] = "CREATE TABLE shared_link (id int);\n",
        ["docs/notes.md"] = "Mentions SHARED_LINK in caps.\n",
        ["src/other.ts"] = "const x = '-dash-term here';\n",
        ["gen/a.txt"] = "shared_link\n",
        ["gen/b.txt"] = "shared_link\n",
        ["docs/menu.md"] = "Cr\u00e8me br\u00fbl\u00e9e\n",
        [".env"] = "shared_link=1\n",
        ["config/secrets/token.txt"] = "shared_link\n",
    };

    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>Makes the search over <see cref="Committed"/>.</summary>
    /// <param name="repo">The repo it answers for.</param>
    /// <param name="commit">Its commit.</param>
    /// <returns>The search.</returns>
    protected abstract ICodeSearch CreateSearch(out ContextRepo repo, out string commit);

    /// <summary>Puts a file in the working tree that the commit does not hold (the fake has no working tree and ignores it).</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="text">The text.</param>
    protected abstract void AddUncommitted(string path, string text);

    /// <summary>A term matches a file's text in any case.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Finds_files_naming_a_term_case_insensitively()
    {
        const string test = nameof(Finds_files_naming_a_term_case_insensitively);
        var search = CreateSearch(out var repo, out var commit);

        var hits = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["shared_link"], Within: null, Max: 100), None), test);
        var both = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["shared_link", "sharedlink"], Within: null, Max: 100), None), test);

        Expect.That(hits.Paths.SequenceEqual(["docs/notes.md", "gen/a.txt", "gen/b.txt", "migrations/002.sql"], StringComparer.Ordinal), test, $"found {string.Join(", ", hits.Paths)}");
        Expect.That(both.Paths.Contains("src/sharing/links.ts", StringComparer.Ordinal), test, $"two terms found {string.Join(", ", both.Paths)}");
    }

    /// <summary>Only the named paths are searched, and a glob character in a path is a character.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Respects_the_paths_filter()
    {
        const string test = nameof(Respects_the_paths_filter);
        var search = CreateSearch(out var repo, out var commit);

        var some = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["shared_link", "sharedlink"], Within: ["migrations/002.sql", "src/sharing/links.ts"], Max: 100), None), test);
        var glob = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["shared_link"], Within: ["migrations/*"], Max: 100), None), test);
        var none = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["shared_link"], Within: [], Max: 100), None), test);

        Expect.That(some.Paths.SequenceEqual(["migrations/002.sql", "src/sharing/links.ts"], StringComparer.Ordinal), test, $"found {string.Join(", ", some.Paths)}");
        Expect.That(glob.Paths.Count == 0, test, $"a glob matched {string.Join(", ", glob.Paths)}");
        Expect.That(none.Paths.Count == 0, test, "an empty filter searched everything");
    }

    /// <summary>A path the repo denies is never returned, even when it is named.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Never_returns_a_denied_path()
    {
        const string test = nameof(Never_returns_a_denied_path);
        var search = CreateSearch(out var repo, out var commit);

        var all = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["shared_link"], Within: null, Max: 100), None), test);
        var named = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["shared_link"], Within: [".env", "config/secrets/token.txt", "docs/notes.md"], Max: 100), None), test);

        Expect.That(!all.Paths.Any(repo.Denies), test, $"found {string.Join(", ", all.Paths)}");
        Expect.That(named.Paths.SequenceEqual(["docs/notes.md"], StringComparer.Ordinal), test, $"named found {string.Join(", ", named.Paths)}");
    }

    /// <summary>A file only the working tree holds is not found.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Honours_a_commit_not_the_working_tree()
    {
        const string test = nameof(Honours_a_commit_not_the_working_tree);
        var search = CreateSearch(out var repo, out var commit);
        AddUncommitted("src/new.ts", "shared_link");

        var hits = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["shared_link"], Within: null, Max: 100), None), test);

        Expect.That(!hits.Paths.Contains("src/new.ts", StringComparer.Ordinal), test, $"found {string.Join(", ", hits.Paths)}");
    }

    /// <summary>Case folding is not ASCII-only: a term in capitals finds the lower-case text and the reverse.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Folds_non_ASCII_case()
    {
        const string test = nameof(Folds_non_ASCII_case);
        var search = CreateSearch(out var repo, out var commit);

        var upper = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["CR\u00c8ME"], Within: null, Max: 100), None), test);
        var mixed = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["br\u00fbl\u00c9e"], Within: null, Max: 100), None), test);

        Expect.That(upper.Paths.SequenceEqual(["docs/menu.md"], StringComparer.Ordinal), test, $"upper found {string.Join(", ", upper.Paths)}");
        Expect.That(mixed.Paths.SequenceEqual(["docs/menu.md"], StringComparer.Ordinal), test, $"mixed found {string.Join(", ", mixed.Paths)}");
    }

    /// <summary>A commit is resolved once to its full sha, and one the clone does not hold is SND-CONTEXT-110.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Resolve_gives_the_full_sha_and_an_unknown_commit_is_110()
    {
        const string test = nameof(Resolve_gives_the_full_sha_and_an_unknown_commit_is_110);
        var search = CreateSearch(out var repo, out var commit);

        var full = Expect.Ok(await search.Resolve(repo, commit[..7], None), test);
        var unknown = await search.Resolve(repo, "0123456789abcdef0123456789abcdef01234567", None);

        Expect.That(string.Equals(full, commit, StringComparison.Ordinal), test, $"resolved {full}");
        Expect.That(unknown.IsError && unknown.Error.Code == ContextErrors.CommitUnknown.Code, test, unknown.IsError ? unknown.Error.Code : "ok");
    }

    /// <summary>A commit the clone does not hold is SND-CONTEXT-110.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Unknown_commit_is_110()
    {
        const string test = nameof(Unknown_commit_is_110);
        var search = CreateSearch(out var repo, out _);

        var result = await search.FilesNaming(new CodeSearchQuery(repo, "0123456789abcdef0123456789abcdef01234567", ["shared_link"], Within: null, Max: 100), None);

        Expect.That(result.IsError && result.Error.Code == ContextErrors.CommitUnknown.Code, test, result.IsError ? result.Error.Code : "ok");
    }

    /// <summary>More matches than the cap are cut and said so.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Caps_and_says_truncated()
    {
        const string test = nameof(Caps_and_says_truncated);
        var search = CreateSearch(out var repo, out var commit);

        var cut = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["shared_link"], Within: null, Max: 2), None), test);
        var exact = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["shared_link"], Within: null, Max: 4), None), test);

        Expect.That(cut is { Truncated: true } && cut.Paths.SequenceEqual(["docs/notes.md", "gen/a.txt"], StringComparer.Ordinal), test, $"cut {string.Join(", ", cut.Paths)} {cut.Truncated}");
        Expect.That(exact is { Truncated: false, Paths.Count: 4 }, test, $"exact {exact.Paths.Count} {exact.Truncated}");
    }

    /// <summary>A term that starts with a dash is searched as text.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_term_starting_with_a_dash_is_a_term_not_a_flag()
    {
        const string test = nameof(A_term_starting_with_a_dash_is_a_term_not_a_flag);
        var search = CreateSearch(out var repo, out var commit);

        var hits = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, ["-dash-term", "--no-index"], Within: null, Max: 100), None), test);

        Expect.That(hits.Paths.SequenceEqual(["src/other.ts"], StringComparer.Ordinal), test, $"found {string.Join(", ", hits.Paths)}");
    }

    /// <summary>A term with a control character, an empty term or an over-long term is refused (SND-CONTEXT-006).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_control_character_in_a_term_is_refused()
    {
        const string test = nameof(A_control_character_in_a_term_is_refused);
        var search = CreateSearch(out var repo, out var commit);

        foreach (var term in new[] { "bad\nterm", "bad\0term", string.Empty, new string('a', 201) })
        {
            var result = await search.FilesNaming(new CodeSearchQuery(repo, commit, [term], Within: null, Max: 100), None);
            Expect.That(result.IsError && result.Error.Code == ContextErrors.QueryInvalid.Code, test, result.IsError ? result.Error.Code : "ok");
        }
    }

    /// <summary>No terms find nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task No_terms_find_nothing()
    {
        const string test = nameof(No_terms_find_nothing);
        var search = CreateSearch(out var repo, out var commit);

        var hits = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, [], Within: null, Max: 100), None), test);

        Expect.That(hits is { Truncated: false, Paths.Count: 0 }, test, "found something");
    }

    /// <summary>More terms or paths than one call carries are searched in chunks and joined.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Many_terms_and_paths_are_searched_in_chunks()
    {
        const string test = nameof(Many_terms_and_paths_are_searched_in_chunks);
        var search = CreateSearch(out var repo, out var commit);
        var terms = Enumerable.Range(0, 250).Select(i => "absent-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("shared_link").ToList();
        var paths = Enumerable.Range(0, 250).Select(i => "none/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("migrations/002.sql").ToList();

        var hits = Expect.Ok(await search.FilesNaming(new CodeSearchQuery(repo, commit, terms, paths, Max: 100), None), test);

        Expect.That(hits.Paths.SequenceEqual(["migrations/002.sql"], StringComparer.Ordinal), test, $"found {string.Join(", ", hits.Paths)}");
    }
}
