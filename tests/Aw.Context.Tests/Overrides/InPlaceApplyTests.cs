using System.Diagnostics;
using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Tests.Review;

namespace Aw.Context.Tests.Overrides;

/// <summary>The applier works on one copy of the graph for the whole fold, and answers exactly what copying it per override did.</summary>
public sealed class InPlaceApplyTests
{
    private static readonly EvidenceRef[] _evidence = [new(RecordKey.From("linear:issue/ENG-1"), "span-a")];

    private static readonly string[] _nodes = ["feature:tabs", "feature:tabs.groups", "feature:layout", "surface:command/switch-tab", "surface:command/close-tab", "feature:gone", "feature:new", "surface:command/new"];

    private static string F(int i) => i.ToString("d5", CultureInfo.InvariantCulture);

    // The sample draft with the shapes the applier treats its own way: an alias, a node key held twice, a test link.
    private static GraphSnapshot Odd()
    {
        var draft = SampleDrafts.Draft();
        return draft with
        {
            Nodes = [.. draft.Nodes, new GraphNode(SampleDrafts.Key("feature:layout"), "Layout again", Provenance.Model, 0.4)],
            Edges = [.. draft.Edges, new GraphEdge(SampleDrafts.Key("code:src/layout/Panes.test.ts"), SampleDrafts.Key("feature:layout"), EdgeType.Tests, Provenance.Model, 0.5)],
            Aliases = [new NodeAlias(SampleDrafts.Key("feature:old-tabs"), SampleDrafts.Key("feature:tabs"))],
        };
    }

    private static MapOverride RandomRow(Random random, int step)
    {
        var at = SampleDrafts.At.AddMinutes(step % 7);   // some rows share a moment, so the id order matters too
        var target = SampleDrafts.Key(_nodes[random.Next(_nodes.Length)]);
        var other = SampleDrafts.Key(_nodes[random.Next(_nodes.Length)]);
        LinkId[] links = [SampleDrafts.Link("code:src/tabs/Groups.ts", EdgeType.Implements, "feature:tabs.groups"), SampleDrafts.Link("code:src/layout/Panes.ts", EdgeType.Implements, "feature:layout"), SampleDrafts.Link("code:src/nowhere.ts", EdgeType.Implements, "feature:tabs")];
        var link = links[random.Next(links.Length)];   // two edges that exist and one that doesn't
        return random.Next(12) switch
        {
            0 => new MapOverride(OverrideAction.Accept, target, "avery", at),
            1 => new MapOverride(OverrideAction.Reject, target, "avery", at),
            2 => new MapOverride(OverrideAction.Reject, target, "avery", at, Rejection: "elsewhere"),
            3 => new MapOverride(OverrideAction.Rename, target, "avery", at, "Renamed " + step.ToString(CultureInfo.InvariantCulture)),
            4 => new MapOverride(OverrideAction.Delete, target, "avery", at),
            5 => new MapOverride(OverrideAction.Move, target, "avery", at, Other: other),
            6 => new MapOverride(OverrideAction.Merge, target, "avery", at, Other: other),
            7 => new MapOverride(OverrideAction.Add, target, "avery", at, "Added", random.Next(2) == 0 ? other : null),
            8 => new MapOverride(OverrideAction.Propose, target, "service:engine", at, "Proposed",
                Proposal: new ProposalPayload("run-1", ContextRepoName.From("aw-split-panes"), target.Kind, random.Next(2) == 0 ? other : null, _evidence, "why")),
            9 => new MapOverride(OverrideAction.Accept, SampleDrafts.Key("code:src/tabs/Groups.ts"), "avery", at, Link: link),
            10 => new MapOverride(random.Next(3) == 0 ? OverrideAction.Rename : OverrideAction.Reject, SampleDrafts.Key("code:src/layout/Panes.ts"), "avery", at, "x", Link: link),
            _ => new MapOverride(OverrideAction.Accept, SampleDrafts.Key("feature:old-tabs"), "avery", at),
        };
    }

    private static void SameAs(AppliedMap expected, AppliedMap actual, string label)
    {
        Assert.True(expected.Graph.Nodes.SequenceEqual(actual.Graph.Nodes), label + ": nodes");
        Assert.True(expected.Graph.Edges.SequenceEqual(actual.Graph.Edges), label + ": edges");
        Assert.True(expected.Graph.Aliases.SequenceEqual(actual.Graph.Aliases), label + ": aliases");
        Assert.True(expected.Graph.Decisions.SequenceEqual(actual.Graph.Decisions), label + ": decisions");
        Assert.True(expected.Orphaned.SequenceEqual(actual.Orphaned), label + ": orphaned");
        Assert.True(expected.Inapplicable.SequenceEqual(actual.Inapplicable), label + ": inapplicable");
    }

    // 600 seeded sequences of every action (an undo among them) on a draft with an alias, a doubled key and missing targets: the
    // applier that keeps one copy must give back exactly what the one that copied per override gave, in the same order.
    [Fact, Trait("Lane", "unit")]
    public void Applying_in_place_answers_exactly_what_copying_per_override_did()
    {
        var random = new Random(4787);
        for (var run = 0; run < 600; run++)
        {
            var rows = new List<MapOverride>();
            for (var step = random.Next(1, 25); step > 0; step--)
            {
                var row = RandomRow(random, rows.Count);
                rows.Add(random.Next(10) == 0 && rows.Count > 0 ? SampleDrafts.Undo(rows[random.Next(rows.Count)], row.At.AddSeconds(1)) : row);
            }

            var draft = run % 2 == 0 ? SampleDrafts.Draft() : Odd();
            SameAs(ReferenceOverrideApplier.Apply(draft, rows), OverrideApplier.Apply(draft, rows), string.Create(CultureInfo.InvariantCulture, $"run {run}"));
        }
    }

    // Counted, not timed: 20k accepts and rejects make one graph at the end, not one copy per row.
    [Fact, Trait("Lane", "unit")]
    public void Twenty_thousand_decisions_make_one_copy_of_the_graph()
    {
        var nodes = Enumerable.Range(0, 20_000).Select(i => new GraphNode(SampleDrafts.Key("feature:f" + F(i)), F(i), Provenance.Model, 0.5)).ToList();
        var draft = new GraphSnapshot(nodes, [], [], []);
        var rows = nodes.Select((n, i) => new MapOverride(i % 2 == 0 ? OverrideAction.Accept : OverrideAction.Reject, n.Key, "avery", SampleDrafts.At)).ToList();
        var copies = 0;

        var applied = OverrideApplier.Apply(draft, rows, () => copies++);

        Assert.Equal(10_000, applied.Graph.Nodes.Count);
        Assert.Equal(1, copies);
    }

    // Adds of nodes that exist and moves or merges that can't apply copy nothing either: only a change the in-place fold can't make does.
    [Fact, Trait("Lane", "unit")]
    public void Adds_that_change_nothing_and_moves_that_cannot_apply_make_no_extra_copy()
    {
        var draft = SampleDrafts.Draft();
        var rows = Enumerable.Range(0, 1_000).Select(i => new MapOverride(OverrideAction.Add, SampleDrafts.Key("feature:tabs"), "avery", SampleDrafts.At.AddSeconds(i), "Tabs"))
            .Append(new MapOverride(OverrideAction.Move, SampleDrafts.Key("feature:layout"), "avery", SampleDrafts.At.AddHours(1), Other: SampleDrafts.Key("feature:nowhere")))
            .Append(new MapOverride(OverrideAction.Merge, SampleDrafts.Key("feature:layout"), "avery", SampleDrafts.At.AddHours(2), Other: SampleDrafts.Key("surface:command/close-tab"))).ToList();
        var copies = 0;

        var applied = OverrideApplier.Apply(draft, rows, () => copies++);

        Assert.Equal(1, copies);
        SameAs(ReferenceOverrideApplier.Apply(draft, rows), applied, "adds and refused edits");
    }

    // The owner's draft: ~24k nodes, ~34k edges, 7.8k overrides decided and 12k more in one file. Heavy lane, generous guard.
    [Fact, Trait("Lane", "heavy")]
    public void An_apply_at_the_size_of_the_owners_draft_takes_seconds()
    {
        const int features = 20_000, files = 4_473;
        var nodes = Enumerable.Range(0, features).Select(i => new GraphNode(SampleDrafts.Key("feature:f" + F(i)), F(i), Provenance.Model, 0.5))
            .Concat(Enumerable.Range(0, files).Select(i => new GraphNode(SampleDrafts.Key("code:src/c" + F(i) + ".ts"), F(i), Provenance.Human, 1))).ToList();
        var edges = Enumerable.Range(0, features).Select(i => new GraphEdge(SampleDrafts.Key("code:src/c" + F(i % files) + ".ts"), SampleDrafts.Key("feature:f" + F(i)), EdgeType.Implements, Provenance.Model, 0.5))
            .Concat(Enumerable.Range(1, 13_778).Select(i => new GraphEdge(SampleDrafts.Key("feature:f" + F(i)), SampleDrafts.Key("feature:f" + F(i / 2)), EdgeType.PartOf, Provenance.Model, 0.5))).ToList();
        var rows = Enumerable.Range(0, features).Select(i => new MapOverride(i % 3 == 0 ? OverrideAction.Reject : OverrideAction.Accept, SampleDrafts.Key("feature:f" + F(i)), "avery", SampleDrafts.At)).ToList();
        var clock = Stopwatch.StartNew();

        var queue = Context.Core.Review.ReviewBacklog.Build("v4", new GraphSnapshot(nodes, edges, [], []), rows);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), string.Create(CultureInfo.InvariantCulture, $"one queue build took {clock.Elapsed.TotalSeconds:F1} s"));
        Assert.DoesNotContain(queue.Entries, e => e.Link is null);
    }
}
