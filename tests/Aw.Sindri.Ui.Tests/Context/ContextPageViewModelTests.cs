using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Context page's skeleton (U9a0): opened empty, no read made, and the four seams its stages fill.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextPageViewModelTests
{
    [Fact]
    public void Opens_empty_and_makes_no_read_before_an_anchor()
    {
        var server = new FakeSindriApi();

        var page = new ContextPageViewModel(server);

        Assert.Equal((Labels.Context, Labels.ContextNoMap, string.Empty), (page.Title, page.Head, page.AnchorText));
        Assert.Equal(0, server.ContextCalls);
    }

    [Fact]
    public void The_seams_are_present_and_typed()
    {
        var page = new ContextPageViewModel(new FakeSindriApi());

        _ = Assert.IsType<MapReviewViewModel>(page.Review);
        _ = Assert.IsType<FileMapViewModel>(page.FileMap);
        _ = Assert.IsType<NodeCardViewModel>(page.Node);
        _ = Assert.IsType<SourcesPanelViewModel>(page.Sources);
    }

    [Fact]
    public void Typing_in_the_anchor_box_notifies_and_reads_nothing()
    {
        var server = new FakeSindriApi();
        var page = new ContextPageViewModel(server);
        var changed = new List<string?>();
        page.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        page.AnchorText = "feature:x";

        Assert.Equal([nameof(ContextPageViewModel.AnchorText)], changed);
        Assert.Equal(0, server.ContextCalls);
    }
}
