using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;

namespace Aw.Context.Tests.Review;

public sealed class ReviewPlanAllTests
{
    private const string Who = "avery";

    private static string F(int i) => "f" + i.ToString("d5", CultureInfo.InvariantCulture);

    private static ReviewDecision On(ReviewVerb verb, string node, string? label = null, string? other = null) =>
        new(verb, SampleDrafts.Key(node), Label: label, Other: other is null ? null : SampleDrafts.Key(other));

    private static ReviewDecision OnLink(ReviewVerb verb, string from, string to) =>
        new(verb, Link: SampleDrafts.Link(from, EdgeType.Implements, to));

    // Each decision planned alone on top of the rows before it: what a file's apply did before it planned the whole file at once.
    private static (List<MapOverride> Rows, List<string> Skipped) OneByOne(IReadOnlyList<ReviewDecision> decisions, ReviewBasis basis)
    {
        var rows = new List<MapOverride>();
        var skipped = new List<string>();
        foreach (var decision in decisions)
        {
            var planned = ReviewPlanner.Plan(decision, basis with { Overrides = [.. basis.Overrides, .. rows] }, Who, SampleDrafts.At);
            if (planned.IsSuccess)
            {
                rows.AddRange(planned.Value);
            }
            else
            {
                skipped.Add(planned.Error.Message);
            }
        }

        return (rows, skipped);
    }

    private static IReadOnlyList<ReviewDecision> Sequence(string name) => name switch
    {
        "nodes, edits and a stale link" =>
        [
            On(ReviewVerb.Accept, "feature:layout"),
            On(ReviewVerb.Accept, "feature:layout"),
            On(ReviewVerb.Reject, "feature:tabs.groups"),
            OnLink(ReviewVerb.Accept, "code:src/tabs/Groups.ts", "feature:tabs.groups"),
            On(ReviewVerb.Accept, "surface:command/switch-tab"),
            On(ReviewVerb.Rename, "surface:command/close-tab", label: "Close"),
            On(ReviewVerb.Reject, "surface:command/close-tab"),
            On(ReviewVerb.Move, "surface:command/switch-tab", other: "feature:layout"),
            On(ReviewVerb.Merge, "feature:layout", other: "feature:tabs"),
            OnLink(ReviewVerb.Accept, "code:src/layout/Panes.ts", "feature:layout"),
            On(ReviewVerb.Reject, "feature:nowhere"),
        ],
        "links first, then their nodes" =>
        [
            OnLink(ReviewVerb.Accept, "code:src/layout/Panes.ts", "feature:layout"),
            On(ReviewVerb.Reject, "feature:layout"),
            OnLink(ReviewVerb.Reject, "code:src/tabs/Groups.ts", "feature:tabs.groups"),
            OnLink(ReviewVerb.Reject, "code:src/tabs/Groups.ts", "feature:tabs.groups"),
            On(ReviewVerb.Reject, "feature:tabs.groups"),
            OnLink(ReviewVerb.Accept, "code:src/tabs/GroupList.ts", "feature:tabs.groups"),
            On(ReviewVerb.Accept, "surface:command/close-tab"),
        ],
        _ => OddSequence(name),
    };

    // The sequences that run on an odd draft (DraftFor) or decide one target again across a node's reject.
    private static IReadOnlyList<ReviewDecision> OddSequence(string name) => name switch
    {
        "an alias that shadows a queued node" =>
        [
            On(ReviewVerb.Accept, "feature:layout"),
            On(ReviewVerb.Accept, "feature:layout"),
            On(ReviewVerb.Reject, "feature:layout"),
        ],
        "a node key held twice" =>
        [
            On(ReviewVerb.Accept, "feature:layout"),
            On(ReviewVerb.Accept, "feature:layout"),
            On(ReviewVerb.Accept, "feature:layout"),
        ],
        "a link decided again after its node is rejected" =>
        [
            OnLink(ReviewVerb.Accept, "code:src/layout/Panes.ts", "feature:layout"),
            On(ReviewVerb.Reject, "feature:layout"),
            OnLink(ReviewVerb.Accept, "code:src/layout/Panes.ts", "feature:layout"),
            OnLink(ReviewVerb.Reject, "code:src/tabs/Groups.ts", "feature:tabs.groups"),
            On(ReviewVerb.Reject, "feature:tabs.groups"),
            OnLink(ReviewVerb.Accept, "code:src/tabs/Groups.ts", "feature:tabs.groups"),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "no such sequence"),
    };

    // The draft each sequence runs on: the shared sample, or one with an odd shape the applier treats its own way.
    private static GraphSnapshot DraftFor(string name)
    {
        var draft = SampleDrafts.Draft();
        return name switch
        {
            "an alias that shadows a queued node" => draft with { Aliases = [new NodeAlias(SampleDrafts.Key("feature:layout"), SampleDrafts.Key("feature:tabs.groups"))] },
            "a node key held twice" => draft with { Nodes = [.. draft.Nodes, draft.Resolve(SampleDrafts.Key("feature:layout"))! with { Label = "Layout again" }] },   // why: the sample holds it
            _ => draft,
        };
    }

    // Planning a whole file at once must decide exactly what planning it entry by entry decides: the same rows, the same skipped lines.
    [Theory, Trait("Lane", "unit")]
    [InlineData("nodes, edits and a stale link")]
    [InlineData("links first, then their nodes")]
    [InlineData("an alias that shadows a queued node")]
    [InlineData("a node key held twice")]
    [InlineData("a link decided again after its node is rejected")]
    public void Planning_a_file_at_once_writes_and_skips_what_planning_each_entry_alone_does(string name)
    {
        var decisions = Sequence(name);
        var basis = new ReviewBasis(DraftFor(name), [SampleDrafts.Row(OverrideAction.Accept, "surface:command/switch-tab", at: SampleDrafts.At.AddDays(-1))]);

        var (rows, skipped) = OneByOne(decisions, basis);
        var (plannedRows, plannedSkipped) = ReviewPlanner.PlanAll(decisions, basis, Who, SampleDrafts.At, TestContext.Current.CancellationToken);

        Assert.Equal(rows, plannedRows);
        Assert.Equal(skipped, plannedSkipped, StringComparer.Ordinal);
    }

    // The owner's file had ~19k decided entries; planning it rebuilt the whole map once per entry and ran for hours. Counted, not timed:
    // accepts and rejects on a plain map are planned against one build, however many there are.
    [Fact, Trait("Lane", "unit")]
    public void Twenty_thousand_accepts_and_rejects_are_planned_against_one_build_of_the_map()
    {
        const int count = 20_000;
        // Built directly: GraphBuilder looks every edge's ends up in its node list, which is itself quadratic at this size.
        var features = Enumerable.Range(0, count).Select(i => new GraphNode(SampleDrafts.Key("feature:" + F(i)), F(i), Provenance.Model, 0.5)).ToList();
        var links = features.Select(n => new GraphEdge(SampleDrafts.Key("code:src/" + n.Label + ".ts"), n.Key, EdgeType.Implements, Provenance.Model, 0.5)).ToList();
        var basis = new ReviewBasis(new GraphSnapshot(features, links, [], []), []);
        var queue = ReviewBacklog.Build("v2@draft", basis.Graph, basis.Overrides);
        var file = new ReviewFile("v2@draft", [.. Enumerable.Range(0, count).Select(i =>
            new ReviewFileEntry(F(i), ReviewGroup.Model, 0.5, Node: SampleDrafts.Key("feature:" + F(i)), Decision: i % 2 == 0 ? ReviewVerb.Accept : ReviewVerb.Reject))]);
        var builds = 0;

        var matched = ReviewFileMatcher.Match(file, queue).Value;
        var (rows, skipped) = ReviewPlanner.PlanAll(matched.Decisions, basis, (Who, SampleDrafts.At), () => builds++, TestContext.Current.CancellationToken);

        Assert.Equal(count, rows.Count);
        Assert.Empty(skipped);
        Assert.Equal(1, builds);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cancelled_plan_stops_before_it_finishes()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        _ = Assert.ThrowsAny<OperationCanceledException>(() => ReviewPlanner.PlanAll([On(ReviewVerb.Accept, "feature:layout")], new ReviewBasis(SampleDrafts.Draft(), []), Who, SampleDrafts.At, cancelled.Token));
    }
}
