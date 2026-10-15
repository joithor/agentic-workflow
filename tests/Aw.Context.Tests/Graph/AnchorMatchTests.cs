using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Graph;

public sealed class AnchorMatchTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("feature:layout.split-pane.orientation", "feature:layout.split-pane.orientation")]
    [InlineData("feature:layout.orientation", "feature:layout.split-pane.orientation")]
    [InlineData("split ORIENTATION", "feature:layout.split-pane.orientation")]
    [InlineData("Tabs", "feature:tabs.tab-manager")]
    public void A_key_an_alias_or_a_label_resolves_to_its_node(string name, string expected)
    {
        var node = AnchorMatch.Resolve(FixtureGraph.Applied(), name);

        Assert.Equal(expected, node.Value.Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_label_two_nodes_share_is_SND_CONTEXT_037_listing_both()
    {
        var graph = new GraphBuilder().Node("feature:a", "Same").Node("feature:b", "same").Build();

        var node = AnchorMatch.Resolve(graph, "Same");

        Assert.Equal(ContextErrors.AmbiguousAnchor.Code, node.Error.Code);
        Assert.Contains("feature:a", node.Error.Message, StringComparison.Ordinal);
        Assert.Contains("feature:b", node.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_name_is_SND_CONTEXT_003_with_suggestions()
    {
        var node = AnchorMatch.Resolve(FixtureGraph.Applied(), "orientation toggles");

        Assert.Equal(ContextErrors.UnknownAnchor.Code, node.Error.Code);
        Assert.Contains("feature:layout.split-pane.orientation", node.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_named_after_more_than_sixteen_other_words_still_anchors()
    {
        var question = "When I change the split pane layout in the settings dialog of the editor window, what else in the app could break, for example Tabs?";

        var anchors = AnchorMatch.FromText(FixtureGraph.Applied(), question);

        Assert.Contains(anchors.Anchors, k => k.Value == "feature:tabs.tab-manager");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_question_naming_a_whole_label_anchors_on_it()
    {
        var anchors = AnchorMatch.FromText(FixtureGraph.Applied(), "What else is affected if I change how split orientation works?");

        Assert.Equal(["feature:layout.split-pane.orientation"], anchors.Anchors.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Contains(anchors.Suggested, k => k.Value == "surface:command/toggle-orientation");
        Assert.True(anchors.Suggested.Count <= 5);
    }

    [Fact, Trait("Lane", "unit")]
    public void Plural_words_and_parenthesized_kinds_still_match_and_the_most_specific_label_wins()
    {
        var anchors = AnchorMatch.FromText(FixtureGraph.Applied(), "Which key splits a pane vertically?");

        Assert.Equal(["action:split-pane.split-vertical"], anchors.Anchors.Select(k => k.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_question_naming_nothing_has_no_anchors_and_no_suggestions()
    {
        var anchors = AnchorMatch.FromText(FixtureGraph.Applied(), "xyzzy plugh");

        Assert.Empty(anchors.Anchors);
        Assert.Empty(anchors.Suggested);
    }
}
