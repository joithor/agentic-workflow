using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// Replay (spec W21, §10 'Replay reproduces the instance'): each event's delta holds the full post-state value of
/// every part the Commit changed, and folding the events from workflow.started rebuilds the row.
/// </summary>
public sealed class EventFoldTests
{
    private static readonly WorkflowInstance _started = StoreRows.Root() with { Version = 1 };

    [Fact, Trait("Lane", "unit")]
    public void Delta_of_a_new_row_holds_every_part()
    {
        var delta = EventFold.Delta(before: null, _started);

        Assert.Equal(new InstanceDelta(1, _started.Frame, _started.Suspended, _started.Loops, _started.Spent, _started.Facts, Outcome: null), delta);
    }

    [Fact, Trait("Lane", "unit")]
    public void Delta_holds_only_the_parts_that_changed()
    {
        var after = StoreRows.Parked(_started, ParkReason.Rounds) with { Version = 2 };

        var delta = EventFold.Delta(_started, after);

        Assert.Equal(new InstanceDelta(2, after.Frame, after.Suspended, Loops: null, Spent: null, Facts: null, Outcome: null), delta);
    }

    [Fact, Trait("Lane", "unit")]
    public void Delta_compares_parts_by_value_not_by_reference()
    {
        var after = _started with
        {
            Frame = _started.Frame with { Deadlines = [.. _started.Frame.Deadlines] },
            Loops = new Dictionary<string, LoopState>(_started.Loops, StringComparer.Ordinal),
            Spent = _started.Spent with { ByState = new Dictionary<string, int>(StringComparer.Ordinal) },
            Facts = new Dictionary<FactKey, FactValue>(_started.Facts),
            Version = 2,
        };

        Assert.Equal(new InstanceDelta(2, Frame: null, Suspended: null, Loops: null, Spent: null, Facts: null, Outcome: null), EventFold.Delta(_started, after));
    }

    [Fact, Trait("Lane", "unit")]
    public void Delta_holds_the_added_and_replaced_facts_only()
    {
        var after = _started with
        {
            Facts = new Dictionary<FactKey, FactValue>(_started.Facts) { [StoreRows.Brief] = StoreRows.Fact('9'), [StoreRows.Findings] = StoreRows.Fact('3') },
            Version = 2,
        };

        var facts = EventFold.Delta(_started, after).Facts!;

        Assert.Equal([StoreRows.Brief, StoreRows.Findings], facts.Keys.Order(Comparer<FactKey>.Create((a, b) => string.CompareOrdinal(a.Value, b.Value))));
        Assert.Equal(StoreRows.Fact('9'), facts[StoreRows.Brief]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Delta_refuses_a_removed_fact()
    {
        var after = _started with { Facts = new Dictionary<FactKey, FactValue>(), Version = 2 };

        _ = Assert.Throws<ArgumentException>(() => EventFold.Delta(_started, after));
    }

    [Fact, Trait("Lane", "unit")]
    public void Apply_of_workflow_started_yields_its_row()
    {
        var started = StoreRows.Started(_started, StoreRows.Key(_started, "start:1"));

        Assert.Same(_started, EventFold.Apply(before: null, started));
    }

    [Fact, Trait("Lane", "unit")]
    public void Apply_replaces_the_changed_parts_and_merges_the_facts()
    {
        var after = _started with
        {
            Spent = new TokenSpend(10, new Dictionary<string, int>(StringComparer.Ordinal) { ["produce"] = 10 }, 0),
            Facts = new Dictionary<FactKey, FactValue>(_started.Facts) { [StoreRows.Findings] = StoreRows.Fact('3') },
            Outcome = OutcomeName.From("done"),
            Version = 2,
        };

        var applied = EventFold.Apply(_started, StoreRows.Changed(_started, after, StoreRows.Key(_started, "run:1.1:a")));

        Assert.True(RowEquality.Same(after, applied));
    }

    [Fact, Trait("Lane", "unit")]
    public void Fold_rebuilds_the_row_through_a_park_and_its_pop()
    {
        var parked = StoreRows.Parked(_started, ParkReason.NoProgress) with { Version = 2 };
        var resumed = _started with { Frame = _started.Frame with { Deadlines = [] }, Version = 3 };
        WorkflowEvent[] events =
        [
            StoreRows.Started(_started, StoreRows.Key(_started, "start:1")),
            StoreRows.Changed(_started, parked, StoreRows.Key(_started, "run:1.1:a")),
            StoreRows.Changed(parked, resumed, StoreRows.Key(_started, "cmd:00000000000000000000000000000001:answer:x")),
        ];

        var folded = EventFold.Fold(events);

        Assert.True(RowEquality.Same(resumed, folded));
        Assert.Empty(folded.Suspended);
    }

    [Fact, Trait("Lane", "unit")]
    public void Fold_refuses_an_event_that_does_not_follow_the_row()
    {
        var parked = StoreRows.Parked(_started, ParkReason.Rounds) with { Version = 2 };
        var skipped = parked with { Version = 3 };

        _ = Assert.Throws<ArgumentException>(() => EventFold.Fold(
        [
            StoreRows.Started(_started, StoreRows.Key(_started, "start:1")),
            StoreRows.Changed(parked, skipped, StoreRows.Key(_started, "run:1.1:a")),
        ]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Fold_refuses_a_log_that_does_not_start_with_workflow_started()
    {
        var parked = StoreRows.Parked(_started, ParkReason.Rounds) with { Version = 2 };

        _ = Assert.Throws<ArgumentException>(() => EventFold.Fold([StoreRows.Changed(_started, parked, StoreRows.Key(_started, "run:1.1:a"))]));
        _ = Assert.Throws<ArgumentException>(() => EventFold.Fold([]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Rows_that_differ_in_any_part_are_not_the_same()
    {
        Assert.True(RowEquality.Same(_started, _started with { Frame = _started.Frame with { Deadlines = [.. _started.Frame.Deadlines] } }));
        Assert.False(RowEquality.Same(_started, _started with { Version = 2 }));
        Assert.False(RowEquality.Same(_started, _started with { Facts = new Dictionary<FactKey, FactValue>() }));
        Assert.False(RowEquality.Same(_started, _started with { Recipe = _started.Recipe with { Children = new Dictionary<RecipeId, RecipeHash>() } }));
        Assert.False(RowEquality.Same(_started, StoreRows.Parked(_started, ParkReason.Rounds)));
    }
}
