using System.Text.RegularExpressions;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Xunit;

namespace Aw.Sindri.Testing.LensPacks;

/// <summary>
/// The contract every <see cref="ILensPacks"/> honours (line spec §5.2, LN-PK lite): the pack the profile chooses for a loop, 'design-five' and 'code-review' when it chooses none; a repo's entry wins over the default;
/// an unknown id, a pack for the other loop and a pack of more than ten lenses are errors, never a fallback; the hash follows the pack's files.
/// </summary>
public abstract partial class LensPacksContract
{
    /// <summary>The packs every run has available.</summary>
    protected static IReadOnlyList<PackSpec> Available { get; } =
    [
        new("design-five", LensPackKind.Design, 5),
        new("design-lite", LensPackKind.Design, 2),
        new("design-wide", LensPackKind.Design, 11),
        new("code-review", LensPackKind.Review, 3),
        new("review-lite", LensPackKind.Review, 1),
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The resolver over <see cref="Available"/>, the profile choosing what the arguments say (null: nothing chosen).</summary>
    /// <param name="designDefault">workflow.packs.lenses.design.default.</param>
    /// <param name="reviewDefault">workflow.packs.lenses.review.default.</param>
    /// <param name="designRepos">workflow.packs.lenses.design.repos: repo to pack id.</param>
    /// <returns>The resolver.</returns>
    protected abstract ILensPacks Create(string? designDefault, string? reviewDefault, IReadOnlyDictionary<string, string> designRepos);

    /// <summary>With nothing chosen, design resolves 'design-five' and review 'code-review'.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task With_nothing_chosen_each_loop_gets_its_built_in_pack()
    {
        var packs = Create(designDefault: null, reviewDefault: null, new Dictionary<string, string>(StringComparer.Ordinal));

        var design = await packs.Resolve(LensPackKind.Design, repo: null, Token);
        var review = await packs.Resolve(LensPackKind.Review, repo: null, Token);

        Expect.True(design.IsSuccess && review.IsSuccess);
        Expect.Equal("design-five", design.Value.Id.Value);
        Expect.Equal(LensPackKind.Design, design.Value.Applies);
        Expect.Sequence(Available[0].LensIds(), design.Value.Lenses.Select(static l => l.Id.Value));
        Expect.Equal("code-review", review.Value.Id.Value);
        Expect.Equal(LensPackKind.Review, review.Value.Applies);
        Expect.Sequence(Available[3].LensIds(), review.Value.Lenses.Select(static l => l.Id.Value));
    }

    /// <summary>A profile naming another built-in pack resolves that pack.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_profile_naming_another_pack_resolves_that_pack()
    {
        var packs = Create("design-lite", "review-lite", new Dictionary<string, string>(StringComparer.Ordinal));

        var design = await packs.Resolve(LensPackKind.Design, repo: null, Token);
        var review = await packs.Resolve(LensPackKind.Review, repo: null, Token);

        Expect.True(design.IsSuccess && review.IsSuccess);
        Expect.Sequence(["design-lite", "review-lite"], [design.Value.Id.Value, review.Value.Id.Value]);
        Expect.Sequence([2, 1], [design.Value.Lenses.Count, review.Value.Lenses.Count]);
    }

    /// <summary>A repo's entry wins over the default; another repo gets the default.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_repos_entry_wins_over_the_default()
    {
        var packs = Create("design-five", reviewDefault: null, new Dictionary<string, string>(StringComparer.Ordinal) { ["web"] = "design-lite" });

        var web = await packs.Resolve(LensPackKind.Design, RepoName.From("web"), Token);
        var other = await packs.Resolve(LensPackKind.Design, RepoName.From("api"), Token);
        var none = await packs.Resolve(LensPackKind.Design, repo: null, Token);

        Expect.True(web.IsSuccess && other.IsSuccess && none.IsSuccess);
        Expect.Sequence(["design-lite", "design-five", "design-five"], [web.Value.Id.Value, other.Value.Id.Value, none.Value.Id.Value]);
    }

    /// <summary>An id that is no built-in pack is an error naming the id.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_pack_id_is_an_error_naming_it()
    {
        var packs = Create("no-such-pack", reviewDefault: null, new Dictionary<string, string>(StringComparer.Ordinal));

        var design = await packs.Resolve(LensPackKind.Design, repo: null, Token);

        Expect.True(design.IsError);
        Expect.True(design.Error.Message.Contains("no-such-pack", StringComparison.Ordinal));
    }

    /// <summary>A pack for the other loop is an error, not a fallback.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_pack_for_the_other_loop_is_an_error()
    {
        var packs = Create("code-review", "design-five", new Dictionary<string, string>(StringComparer.Ordinal));

        var design = await packs.Resolve(LensPackKind.Design, repo: null, Token);
        var review = await packs.Resolve(LensPackKind.Review, repo: null, Token);

        Expect.True(design.IsError && review.IsError);
    }

    /// <summary>A pack of more than ten lenses is an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_pack_of_more_than_ten_lenses_is_an_error()
    {
        var packs = Create("design-wide", reviewDefault: null, new Dictionary<string, string>(StringComparer.Ordinal));

        var design = await packs.Resolve(LensPackKind.Design, repo: null, Token);

        Expect.True(design.IsError);
        Expect.True(design.Error.Message.Contains("11", StringComparison.Ordinal));
    }

    /// <summary>The hash is 'sha256:' and 64 hex digits, the same on every call and different between packs.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_hash_is_stable_and_differs_between_packs()
    {
        var five = await Create("design-five", reviewDefault: null, new Dictionary<string, string>(StringComparer.Ordinal)).Resolve(LensPackKind.Design, repo: null, Token);
        var again = await Create("design-five", reviewDefault: null, new Dictionary<string, string>(StringComparer.Ordinal)).Resolve(LensPackKind.Design, repo: null, Token);
        var lite = await Create("design-lite", reviewDefault: null, new Dictionary<string, string>(StringComparer.Ordinal)).Resolve(LensPackKind.Design, repo: null, Token);

        Expect.True(Sha256.IsMatch(five.Value.Hash));
        Expect.Equal(five.Value.Hash, again.Value.Hash);
        Expect.True(!string.Equals(five.Value.Hash, lite.Value.Hash, StringComparison.Ordinal));
    }

    [GeneratedRegex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha256 { get; }
}
