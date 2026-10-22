using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The code lens follows the selection (S-9): a chosen file shows the callers and callees panels, any other node or Esc hides them.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextCodeSelectionTests
{
    private const string Manager = "feature:tabs.tab-manager";
    private const string ManagerFile = "code:src/tabs/TabManager.ts";
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ContextPageViewModel> Open() => (await OpenWith()).Page;

    private static async Task<(ContextPageViewModel Page, FakeSindriApi Api)> OpenWith()
    {
        var api = new FakeSindriApi { CodeAnswer = RouteAnswer.Api("context-code", "edges-and-lexical") };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var page = new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { })) { AnchorText = Manager };
        await page.Submit(Token);
        Assert.NotNull(page.Map);
        return (page, api);
    }

    [Fact]
    public async Task Choosing_a_file_reads_the_code_lens_and_shows_the_panels()
    {
        var (page, api) = await OpenWith();
        Assert.False(page.Code.HasPanels);

        await page.Select(ManagerFile, Token);

        Assert.True(page.Code.HasPanels);
        Assert.Equal([ManagerFile], Assert.Single(api.CodeQueries).Anchors);
    }

    [Fact]
    public async Task Choosing_a_node_that_is_not_a_file_hides_the_panels()
    {
        var page = await Open();
        await page.Select(ManagerFile, Token);

        await page.Select(Manager, Token);

        Assert.False(page.Code.HasPanels);
    }

    [Fact]
    public async Task Esc_clears_the_panels()
    {
        var page = await Open();
        await page.Select(ManagerFile, Token);

        page.CloseInspector();

        Assert.False(page.Code.HasPanels);
    }

    [Fact]
    public async Task A_slow_read_for_the_file_left_behind_never_draws_over_the_next_selection()
    {
        var (page, api) = await OpenWith();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.CodeGate = gate;
        var first = page.Select(ManagerFile, Token);
        api.CodeGate = null;

        await page.Select(Manager, Token);
        gate.SetResult();
        await first;

        Assert.False(page.Code.HasPanels);
        Assert.False(page.Code.HasNotice);
    }

    [Fact]
    public async Task A_failed_read_for_the_file_left_behind_says_nothing_over_the_next_selection()
    {
        var (page, api) = await OpenWith();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.CodeGate = gate;
        api.CodeAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-CONTEXT-009", "down");
        var first = page.Select(ManagerFile, Token);
        api.CodeGate = null;
        api.CodeAnswer = RouteAnswer.Api("context-code", "edges-and-lexical");

        await page.Select(ManagerFile, Token);
        gate.SetResult();
        await first;

        Assert.True(page.Code.HasPanels);
        Assert.False(page.Code.HasNotice);
    }

    [Fact]
    public async Task A_refused_read_says_the_code_lens_is_unavailable()
    {
        var (page, api) = await OpenWith();
        api.CodeAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-CONTEXT-009", "down");

        await page.Select(ManagerFile, Token);

        Assert.False(page.Code.HasPanels);
        Assert.Equal(Labels.ContextCodeUnavailable, page.Code.Notice);
    }
}
