using Aw.Context.Core.Overrides;
using Aw.Context.Core.Review;

namespace Aw.Context.Tests.Review;

public sealed class ReviewFileTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_exported_file_lists_every_undecided_entry_except_orphans_and_decides_none()
    {
        var queue = ReviewBacklog.Build("v14@7c3e1a2", SampleDrafts.Draft(), [SampleDrafts.Row(OverrideAction.Rename, "feature:gone", "Gone")]);

        var file = ReviewFile.Of(queue);

        Assert.Equal("v14@7c3e1a2", file.Draft);
        Assert.Equal(queue.Entries.Count - 1, file.Entries.Count);
        Assert.All(file.Entries, e => Assert.Null(e.Decision));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_entry_carries_its_link_and_no_node_and_a_node_entry_the_reverse()
    {
        var file = ReviewFile.Of(ReviewBacklog.Build("v1@a", SampleDrafts.Draft(), []));

        var link = file.Entries.First(e => e.Link is not null);
        var node = file.Entries.First(e => e.Link is null);
        Assert.Null(link.Node);
        Assert.Equal("surface:command/close-tab", node.Node?.Value);
    }
}
