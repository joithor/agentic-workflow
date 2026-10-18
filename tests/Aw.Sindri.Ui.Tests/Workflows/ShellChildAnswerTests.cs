using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Following "Answer in the child" in the shell: the child's frame, or the parent's frame with focus asked for its Children panel.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellChildAnswerTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Workflows.Groups.Count > 0, Token);
        return shell;
    }

    [Fact]
    public Task With_a_child_it_opens_the_childs_frame() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var served = FixtureInstances.Summary("escalated-child");

        shell.FollowChildAnswer(ChildAnswerViewModel.ForInstance(served.Address, served.Answer!.Child));

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, served.Answer.Child), shell.SheetLink);
    }, Token);

    [Fact]
    public Task Without_a_child_it_opens_the_parents_frame_and_asks_for_its_children_panel() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var detail = FixtureInstances.Detail("project-build");
        shell.OpenInstance(detail.Address, detail.Id);
        await ShellWait.Until(shell, () => shell.Instance.Phase == FramePhase.Shown && shell.Instance.Address == detail.Address, Token);

        shell.FollowChildAnswer(ChildAnswerViewModel.ForInstance(detail.Address, childAddress: null));

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, detail.Address), shell.SheetLink);
        Assert.Equal(FrameFocus.Children, shell.Instance.WantedFocus!.Target);
    }, Token);

    [Fact]
    public Task Without_a_child_and_with_no_children_to_draw_focus_goes_to_the_first_read_action_at_once() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var detail = FixtureInstances.Detail("project-build");
        var bare = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "project-build"))!;
        bare["children"] = new JsonArray();
        _server.InstanceBody = id => string.Equals(id, detail.Id, StringComparison.Ordinal) ? new RouteAnswer(HttpStatusCode.OK, bare.ToJsonString()) : null;
        shell.OpenInstance(detail.Address, detail.Id);
        await ShellWait.Until(shell, () => shell.Instance.Phase == FramePhase.Shown && shell.Instance.Address == detail.Address, Token);

        shell.FollowChildAnswer(ChildAnswerViewModel.ForInstance(detail.Address, childAddress: null));

        var wanted = shell.Instance.WantedFocus!.Target;
        Assert.NotEqual(FrameFocus.Children, wanted);   // no Children panel is drawn: asking for it would wait out the retries on nothing
        Assert.Equal(shell.Instance.InitialFocus, wanted);
    }, Token);
}
