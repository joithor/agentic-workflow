using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Context page's skeleton (U9a0): opened empty, no read made, and the four seams its stages fill.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextPageViewModelTests
{
    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => null, clipboard, clock, new CatalogViewModel(clipboard, clock)));
    }

    [Fact]
    public void Opens_empty_with_no_map_words_and_makes_no_read_before_an_anchor()
    {
        var server = new FakeSindriApi();

        var page = Page(server);

        Assert.Equal((Labels.Context, string.Empty, false, string.Empty), (page.Title, page.Head, page.HasHead, page.AnchorText));
        Assert.Equal(0, server.ContextCalls);
    }

    [Fact]
    public void The_seams_are_present_and_typed()
    {
        var page = Page(new FakeSindriApi());

        _ = Assert.IsType<MapReviewViewModel>(page.Review);
        _ = Assert.IsType<FileMapViewModel>(page.FileMap);
        _ = Assert.IsType<NodeCardViewModel>(page.Node);
        _ = Assert.IsType<SourcesPanelViewModel>(page.Sources);
    }

    [Fact]
    public void Typing_in_the_anchor_box_notifies_and_reads_nothing()
    {
        var server = new FakeSindriApi();
        var page = Page(server);
        var changed = new List<string?>();
        page.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        page.AnchorText = "feature:x";

        Assert.Equal([nameof(ContextPageViewModel.AnchorText)], changed);
        Assert.Equal(0, server.ContextCalls);
    }
}
