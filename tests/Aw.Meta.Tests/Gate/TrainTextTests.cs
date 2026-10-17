using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;

namespace Aw.Meta.Tests.Gate;

public sealed class TrainTextTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_queue_keeps_its_order_except_where_a_merge_order_rule_says_otherwise()
    {
        Assert.Equal([5, 7, 9], TrainOrder.Sort([5, 7, 9], []));
        Assert.Equal([7, 9, 5], TrainOrder.Sort([5, 7, 9], [new MergeOrderRule(9, 5), new MergeOrderRule(7, 9)]));
        Assert.Equal([5, 7], TrainOrder.Sort([5, 7], [new MergeOrderRule(99, 5)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_summary_lists_position_pr_base_and_result_in_train_order_with_the_left_out_after()
    {
        var lines = TrainText.Summary(
            "abcdef0123",
            [
                new TrainEntry(0, 14, "main+#11", TrainState.Dropped, "conflicts with #11 at 1234567"),
                new TrainEntry(2, 12, TrainText.BaseOf([new StackedPr(11, "h11")]), TrainState.Fail),
                new TrainEntry(1, 11, TrainText.BaseOf([]), TrainState.Pass),
            ]);

        Assert.Equal(
            [
                "TRAIN main abcdef0: position, PR, base, RESULT",
                "  1  #11  main      PASS",
                "  2  #12  main+#11  FAIL",
                "  -  #14  main+#11  DROPPED conflicts with #11 at 1234567",
            ],
            lines);
    }
}
