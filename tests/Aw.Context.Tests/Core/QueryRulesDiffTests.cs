using Aw.Context.Core;
using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Core;

public sealed class QueryRulesDiffTests
{
    private const string Base = "0123456789abcdef0123456789abcdef01234567";
    private const string Head = "fedcba9876543210fedcba9876543210fedcba98";

    private static ContextQuery Diff(string range, ContextRepoName? repo = null) => new(Text: null, Diff: DiffRange.Parse(range).Value, Repo: repo);

    [Fact, Trait("Lane", "unit")]
    public void Diff_is_a_fifth_shape_and_two_shapes_are_006()
    {
        var ok = QueryRules.Check(Diff($"{Base}..{Head}"), new ContextSettings());
        var two = QueryRules.Check(new ContextQuery("x", Diff: new DiffRange(Base, Head)), new ContextSettings());
        var withNode = QueryRules.Check(new ContextQuery(Text: null, Node: "feature:a", Diff: new DiffRange(Base, Head)), new ContextSettings());

        Assert.True(ok.IsSuccess);
        Assert.Empty(ok.Value.Terms);
        Assert.Equal(ContextErrors.QueryInvalid.Code, two.Error.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, withNode.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("main..0123456789abcdef0123456789abcdef01234567")]
    [InlineData("HEAD~1..0123456789abcdef0123456789abcdef01234567")]
    [InlineData("0123456789abcdef...fedcba9876543210")]
    [InlineData("0123456789abcdef..")]
    [InlineData("..fedcba9876543210")]
    [InlineData("0123456789ABCDEF..fedcba9876543210")]
    [InlineData("0123456..fedcba9876543210 ")]
    [InlineData("0123456..fedcba9876543210\n")]
    [InlineData("012345..fedcba9876543210")]
    [InlineData("")]
    public void Diff_range_needs_two_shas_joined_by_two_dots(string range)
    {
        var parsed = DiffRange.Parse(range);

        Assert.Equal(ContextErrors.QueryInvalid.Code, parsed.Error.Code);
        Assert.Contains("resolve the refs to shas first", parsed.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_range_of_shas_parses_to_its_two_sides_and_prints_back()
    {
        var parsed = DiffRange.Parse($"{Base[..7]}..{Head}").Value;

        Assert.Equal((Base[..7], Head), (parsed.Base, parsed.Head));
        Assert.Equal($"{Base[..7]}..{Head}", parsed.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_range_built_in_process_with_a_ref_name_is_006_at_check()
    {
        var error = QueryRules.Check(new ContextQuery(Text: null, Diff: new DiffRange("main", Head)), new ContextSettings()).Error;

        Assert.Equal(ContextErrors.QueryInvalid.Code, error.Code);
        Assert.Contains("resolve the refs to shas first", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Repo_without_diff_is_006()
    {
        var error = QueryRules.Check(new ContextQuery("split panes", Repo: ContextRepoName.From("aw-split-panes")), new ContextSettings()).Error;

        Assert.Equal(ContextErrors.QueryInvalid.Code, error.Code);
        Assert.Contains("repo", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Repo_with_diff_is_accepted()
        => Assert.True(QueryRules.Check(Diff($"{Base}..{Head}", ContextRepoName.From("aw-split-panes")), new ContextSettings()).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void Diff_sha_longer_than_64_characters_is_006()
    {
        var long_ = new DiffRange(new string('a', QueryRules.MaxEncodedText), Head);

        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.Check(new ContextQuery(Text: null, Diff: long_), new ContextSettings()).Error.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, DiffRange.Parse(long_.ToString()).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_new_codes_110_and_111_are_attention_errors_with_their_kinds()
    {
        Assert.Equal(("SND-CONTEXT-110", Kernel.ErrorKind.NotFound), (ContextErrors.CommitUnknown.Code, ContextErrors.CommitUnknown.Kind));
        Assert.Equal(("SND-CONTEXT-111", Kernel.ErrorKind.Invalid), (ContextErrors.NoMergeBase.Code, ContextErrors.NoMergeBase.Kind));
        Assert.Contains(ContextErrors.CommitUnknown.Code, ContextErrors.Attention);
        Assert.Contains(ContextErrors.NoMergeBase.Code, ContextErrors.Attention);
    }
}
