using Aw.Kernel.Testing;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>Which lenses a round queues and which it records as not applicable (line spec §5.2 'when').</summary>
[Lane(TestLane.Unit)]
public sealed class LensSelectionTests
{
    private static LensPack Pack(params (string Id, LensWhen When)[] lenses) =>
        new(LensPackId.From("pack"), "sha256:" + new string('a', 64), LensPackKind.Review, [.. lenses.Select(static l => new PackLens(LensId.From(l.Id), l.Id, "prompt", l.When))]);

    private static LensWhen Paths(params string[] globs) => new([], globs);

    [Fact]
    public void An_always_lens_is_queued_whatever_the_subject()
    {
        var selection = LensSelection.Of(Pack(("aa", LensWhen.Always)), surfaceKinds: [], changedPaths: []);

        Assert.Equal(["aa"], selection.Queued.Select(static q => q.Value), StringComparer.Ordinal);
        Assert.Empty(selection.NotApplicable);
    }

    [Fact]
    public void A_paths_lens_that_matches_a_changed_path_is_queued()
    {
        var selection = LensSelection.Of(Pack(("db", Paths("**/migrations/**"))), surfaceKinds: null, ["src/app.cs", "db/migrations/001_add.sql"]);

        Assert.Equal(["db"], selection.Queued.Select(static q => q.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_paths_lens_that_matches_nothing_is_not_applicable_with_the_reason()
    {
        var selection = LensSelection.Of(Pack(("aa", LensWhen.Always), ("db", Paths("**/migrations/**", "**/*.sql"))), surfaceKinds: null, ["src/app.cs"]);

        Assert.Equal(["aa"], selection.Queued.Select(static q => q.Value), StringComparer.Ordinal);
        var (lens, reason) = Assert.Single(selection.NotApplicable);
        Assert.Equal("db", lens.Value);
        Assert.Equal("no changed path matches **/migrations/**, **/*.sql", reason);
    }

    [Fact]
    public void Queued_and_not_applicable_both_keep_pack_order()
    {
        var selection = LensSelection.Of(Pack(("aa", Paths("*.md")), ("bb", LensWhen.Always), ("cc", Paths("*.md")), ("dd", LensWhen.Always)), surfaceKinds: null, ["x.cs"]);

        Assert.Equal(["bb", "dd"], selection.Queued.Select(static q => q.Value), StringComparer.Ordinal);
        Assert.Equal(["aa", "cc"], selection.NotApplicable.Select(static n => n.Lens.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_surface_kinds_lens_needs_a_surface_of_that_kind_ignoring_case()
    {
        var pack = Pack(("ui", new LensWhen(["screen", "flow"], [])));

        Assert.Equal(["ui"], LensSelection.Of(pack, ["Flow", "api"], changedPaths: null).Queued.Select(static q => q.Value), StringComparer.Ordinal);
        var none = LensSelection.Of(pack, ["api"], changedPaths: null);
        Assert.Empty(none.Queued);
        Assert.Equal("no surface of kind screen, flow in the scope", Assert.Single(none.NotApplicable).Reason);
    }

    [Fact]
    public void A_subject_that_does_not_say_selects_every_conditional_lens()
    {
        var pack = Pack(("db", Paths("*.sql")), ("ui", new LensWhen(["screen"], [])));

        Assert.Equal(["db", "ui"], LensSelection.Of(pack, surfaceKinds: null, changedPaths: null).Queued.Select(static q => q.Value), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("**/migrations/**", "migrations/001.sql", true)]
    [InlineData("**/migrations/**", "a/b/migrations/001.sql", true)]
    [InlineData("**/migrations/**", "a/migration/001.sql", false)]
    [InlineData("**/*.sql", "x.sql", true)]
    [InlineData("**/*.sql", "a/b/x.sql", true)]
    [InlineData("*.sql", "a/x.sql", false)]
    [InlineData("src/*/x.cs", "src/a/x.cs", true)]
    [InlineData("src/*/x.cs", "src/a/b/x.cs", false)]
    [InlineData("a?c.txt", "abc.txt", true)]
    [InlineData("a?c.txt", "ac.txt", false)]
    [InlineData("**/schema.*", "db/schema.rb", true)]
    [InlineData("**/Schema.*", "db/schema.rb", false)]
    public void Globs_match_by_segment(string glob, string path, bool matches)
    {
        var selection = LensSelection.Of(Pack(("ll", Paths(glob))), surfaceKinds: null, [path]);

        Assert.Equal(matches, selection.Queued.Count == 1);
    }
}
