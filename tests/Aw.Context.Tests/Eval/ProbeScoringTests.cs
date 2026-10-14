using Aw.Context.Core.Eval;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Eval;

public sealed class ProbeScoringTests
{
    [Fact, Trait("Lane", "unit")]
    public void Keys_come_in_answer_order_with_path_nodes_files_and_citations_and_no_repeats()
    {
        var affects = Answers.Item("feature:tabs.tab-manager") with
        {
            Path = ["feature:layout.split-pane.orientation", "writes thing:pane-layout", "reads action:tab-manager.restore-layout"],
        };
        var result = Answers.With(Answers.Map(("C2", "repo:aw-split-panes/docs/x.md")),
            ("graph", Answers.Item("feature:layout.split-pane.orientation")),
            ("affects", affects),
            ("code", Answers.Item("code:src/layout/SplitPane.ts")),
            ("records", Answers.Item(node: null, "C2")));

        Assert.Equal(
            ["feature:layout.split-pane.orientation", "map:aw-split-panes/v1", "feature:tabs.tab-manager", "thing:pane-layout", "action:tab-manager.restore-layout",
             "file:src/layout/SplitPane.ts", "file:docs/x.md"],
            ProbeScoring.Keys(result));
    }

    [Fact, Trait("Lane", "unit")]
    public void Precision_at_5_is_hits_in_the_first_five_over_five()
    {
        var probe = Answers.Probe("p", nodes: ["feature:a", "feature:c"], files: ["src/x.ts"]);
        var result = Answers.Of(("graph", Answers.Item("feature:a")), ("graph", Answers.Item("feature:b")), ("graph", Answers.Item("feature:c")),
            ("graph", Answers.Item("feature:d")), ("code", Answers.Item("code:src/x.ts")), ("graph", Answers.Item("feature:e")));

        var score = ProbeScoring.Score(probe, result);

        // keys: a, map citation, b, c, d → hits a, c
        Assert.Equal(0.4, score.PrecisionAt5, 6);
        Assert.Equal(["feature:a", "feature:c"], score.Hits);
        Assert.Equal(5, score.Top.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Fewer_than_five_keys_divide_by_what_came_back_and_none_scores_zero()
    {
        var probe = Answers.Probe("p", records: ["linear:issue/1"]);

        var one = ProbeScoring.Score(probe, Answers.With(new Dictionary<string, Citation>(StringComparer.Ordinal) { ["C1"] = Answers.Cite("linear:issue/1") },
            ("records", Answers.Item(node: null))));
        var none = ProbeScoring.Score(probe, Answers.Of());

        Assert.Equal(1.0, one.PrecisionAt5, 6);
        Assert.Equal(0.0, none.PrecisionAt5, 6);
        Assert.Equal(1.0, none.CitationCorrectness, 6);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_absent_node_the_answer_names_is_listed()
    {
        var probe = Answers.Probe("p", nodes: ["feature:a"], absent: ["feature:sibling"]);

        var score = ProbeScoring.Score(probe, Answers.Of(("graph", Answers.Item("feature:a")), ("affects", Answers.Item("feature:sibling"))));

        Assert.Equal(["feature:sibling"], score.AbsentReturned);
    }

    [Fact, Trait("Lane", "unit")]
    public void Citation_correctness_is_the_share_of_items_whose_markers_all_resolve()
    {
        var probe = Answers.Probe("p", nodes: ["feature:a"]);

        var score = ProbeScoring.Score(probe, Answers.Of(("graph", Answers.Item("feature:a")), ("graph", Answers.Item("feature:b", "C1", "C9"))));

        Assert.Equal(0.5, score.CitationCorrectness, 6);
    }
}
