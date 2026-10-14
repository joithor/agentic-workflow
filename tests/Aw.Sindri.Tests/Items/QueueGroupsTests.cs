using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Items;

/// <summary>The queue's group set (API spec §3.6, owner decision: four groups) and the sentences each list answers with when empty (UI spec E-18).</summary>
[Lane(TestLane.Unit)]
public sealed class QueueGroupsTests
{
    [Fact]
    public void Order_is_the_four_groups_of_the_spec() =>
        Expect.Sequence([QueueGroup.WaitingOnYou, QueueGroup.WithYou, QueueGroup.Running, QueueGroup.Queued], QueueGroups.Order);

    [Fact]
    public void Every_open_state_maps_into_Order_and_closed_ones_leave_the_queue()
    {
        foreach (var state in Enum.GetValues<ItemState>())
        {
            var group = QueueGroups.GroupOf(state);
            var closed = state is ItemState.Done or ItemState.Cancelled;

            Expect.True(closed ? group is null : group is { } g && QueueGroups.Order.Contains(g));
        }

        Expect.Equal(QueueGroup.WaitingOnYou, QueueGroups.GroupOf(ItemState.Parked));
    }

    [Fact]
    public void Every_group_has_a_sentence()
    {
        foreach (var group in Enum.GetValues<QueueGroup>())
        {
            Expect.True(EmptyText.Group(group).EndsWith('.'));
        }

        Expect.Equal("Nothing is waiting on you.", EmptyText.Group(QueueGroup.WaitingOnYou));
        Expect.Equal("Nothing parked.", EmptyText.Group(QueueGroup.Parked));
        Expect.Equal("Nothing in the queue. Add one with `sindri item add`.", EmptyText.Queue());
    }

    [Fact]
    public void Items_sentence_names_its_filters()
    {
        Expect.Equal("No items.", EmptyText.Items(state: null, repo: null));
        Expect.Equal("No needs-decision items.", EmptyText.Items(ItemState.NeedsDecision, repo: null));
        Expect.Equal("No items in agentic-workflow.", EmptyText.Items(state: null, RepoName.From("agentic-workflow")));
        Expect.Equal("No done items in agentic-workflow.", EmptyText.Items(ItemState.Done, RepoName.From("agentic-workflow")));
        Expect.Equal("Nothing has happened to this item yet.", EmptyText.Timeline());
    }
}
