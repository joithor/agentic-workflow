using Aw.Context.Core;
using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Core;

public sealed class QueryRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Text_becomes_distinct_lower_case_terms_and_the_budget_is_capped()
    {
        var valid = QueryRules.Check(new ContextQuery("Split pane: ORIENTATION, split-pane!", Budget: 50_000), new ContextSettings { MaxBudget = 6_000 });

        Assert.Equal(["split", "pane", "orientation"], valid.Value.Terms);
        Assert.Equal(6_000, valid.Value.Budget);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_budget_gets_the_default() =>
        Assert.Equal(QueryRules.DefaultBudget, QueryRules.Check(new ContextQuery("x"), new ContextSettings()).Value.Budget);

    [Theory, Trait("Lane", "unit")]
    [InlineData("  ,; ")]
    [InlineData(null)]
    public void A_question_without_words_or_shape_is_006(string? text)
    {
        var checkedQuery = QueryRules.Check(new ContextQuery(text), new ContextSettings());

        Assert.Equal(ContextErrors.QueryInvalid.Code, checkedQuery.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_shapes_a_bad_depth_or_a_bad_budget_are_006()
    {
        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.Check(new ContextQuery("x", Node: "feature:a"), new()).Error.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.Check(new ContextQuery("x", Depth: 5), new()).Error.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.Check(new ContextQuery("x", Budget: 0), new()).Error.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.Check(new ContextQuery(new string('a', 2_001)), new()).Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("ENG-1", null)]
    [InlineData(null, "src/a.ts")]
    public void Item_and_symbol_anchors_are_unknown_until_their_phases(string? item, string? symbol) =>
        Assert.Equal(ContextErrors.UnknownAnchor.Code, QueryRules.Check(new ContextQuery(Text: null, Node: null, item, symbol), new()).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_node_anchor_passes_with_no_terms_of_its_own_and_an_empty_one_is_006()
    {
        var node = QueryRules.Check(new ContextQuery(Text: null, Node: "feature:layout.split-pane"), new());
        var empty = QueryRules.Check(new ContextQuery(Text: null, Node: " "), new());

        Assert.Empty(node.Value.Terms);
        Assert.Equal(ContextErrors.QueryInvalid.Code, empty.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Terms_stop_at_sixteen_and_words_at_64_characters()
    {
        var terms = QueryRules.Terms(string.Join(' ', Enumerable.Range(0, 20).Select(i => "w" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))) + " " + new string('z', 70));

        Assert.Equal(QueryRules.MaxTerms, terms.Count);
        Assert.Equal(64, QueryRules.Terms(new string('z', 70)).Single().Length);
    }

    [Fact, Trait("Lane", "unit")]
    public void Words_take_the_cap_of_the_caller_and_Terms_keeps_sixteen()
    {
        var text = string.Join(' ', Enumerable.Range(0, 40).Select(i => "w" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(40, QueryRules.Words(text, 256).Count);
        Assert.Equal(5, QueryRules.Words(text, 5).Count);
        Assert.Equal(QueryRules.Words(text, QueryRules.MaxTerms), QueryRules.Terms(text));
    }
}
