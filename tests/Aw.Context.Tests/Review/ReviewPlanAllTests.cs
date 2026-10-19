using System.Diagnostics;
using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Testing.Graph;

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
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "no such sequence"),
    };

    // Planning a whole file at once must decide exactly what planning it entry by entry decides: the same rows, the same skipped lines.
    [Theory, Trait("Lane", "unit")]
    [InlineData("nodes, edits and a stale link")]
    [InlineData("links first, then their nodes")]
    public void Planning_a_file_at_once_writes_and_skips_what_planning_each_entry_alone_does(string name)
    {
        var decisions = Sequence(name);
        var basis = new ReviewBasis(SampleDrafts.Draft(), [SampleDrafts.Row(OverrideAction.Accept, "surface:command/switch-tab", at: SampleDrafts.At.AddDays(-1))]);

        var (rows, skipped) = OneByOne(decisions, basis);
        var (plannedRows, plannedSkipped) = ReviewPlanner.PlanAll(decisions, basis, Who, SampleDrafts.At, TestContext.Current.CancellationToken);

        Assert.Equal(rows, plannedRows);
        Assert.Equal(skipped, plannedSkipped, StringComparer.Ordinal);
    }

    // The owner's file had ~19k decided entries; planning it rebuilt the whole map once per entry and ran for hours. Linear, this is well under a second.
    [Fact, Trait("Lane", "unit")]
    public void A_file_of_twenty_thousand_decisions_is_matched_and_planned_in_seconds_not_hours()
    {
        const int count = 20_000;
        var builder = new GraphBuilder();
        for (var i = 0; i < count; i++)
        {
            _ = builder.Node("feature:" + F(i), F(i), Provenance.Model, 0.5)
                .Edge("code:src/" + F(i) + ".ts", EdgeType.Implements, "feature:" + F(i), provenance: Provenance.Model, confidence: 0.5);
        }

        var basis = new ReviewBasis(builder.Build(), []);
        var queue = ReviewBacklog.Build("v2@draft", basis.Graph, basis.Overrides);
        var file = new ReviewFile("v2@draft", [.. Enumerable.Range(0, count).Select(i =>
            new ReviewFileEntry(F(i), ReviewGroup.Model, 0.5, Node: SampleDrafts.Key("feature:" + F(i)), Decision: i % 2 == 0 ? ReviewVerb.Accept : ReviewVerb.Reject))]);
        var clock = Stopwatch.StartNew();

        var matched = ReviewFileMatcher.Match(file, queue).Value;
        var (rows, skipped) = ReviewPlanner.PlanAll(matched.Decisions, basis, Who, SampleDrafts.At, TestContext.Current.CancellationToken);

        clock.Stop();
        Assert.Equal(count, rows.Count);
        Assert.Empty(skipped);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), string.Create(CultureInfo.InvariantCulture, $"matching and planning {count} decisions took {clock.Elapsed}"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cancelled_plan_stops_before_it_finishes()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        _ = Assert.ThrowsAny<OperationCanceledException>(() => ReviewPlanner.PlanAll([On(ReviewVerb.Accept, "feature:layout")], new ReviewBasis(SampleDrafts.Draft(), []), Who, SampleDrafts.At, cancelled.Token));
    }
}
