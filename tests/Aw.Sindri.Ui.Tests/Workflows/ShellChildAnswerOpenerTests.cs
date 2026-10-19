using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// "Answer in the child" from a needs-you row, a strip row and a Workflows row: which frame opens (a needs-you row is served the
/// instance it stands for, never the child) and which row Esc returns to.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ShellChildAnswerOpenerTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected(string? subject, string scenario = "park")
    {
        var park = JsonNode.Parse(GoldenFiles.Api("needs-you", scenario))!;
        var row = park["rows"]![0]!;
        row["via"] = "child";
        if (subject is not null)
        {
            row["subject"]!["id"] = subject;
        }

        _server.NeedsYouView = new RouteAnswer(HttpStatusCode.OK, park.ToJsonString());
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Workflows.Groups.Count > 0 && shell.NeedsYou.Seen.Rows.Count > 0, Token);
        return shell;
    }

    // The first row of the list the fake serves, so a needs-you subject can name a row the shell holds.
    private static (string Address, string Id) Served()
    {
        var first = GoldenFiles.Node("workflow-instances", "root")["instances"]![0]!;
        return (first["address"]!.GetValue<string>(), first["id"]!.GetValue<string>());
    }

    private static InstanceRowViewModel AnyRow(ShellViewModel shell) => shell.Workflows.Groups.SelectMany(g => g.Rows).First();

    private static ChildAnswerViewModel OfNeedsYou(ShellViewModel shell) => shell.NeedsYou.Seen.Rows[0].ChildAnswer!;

    [Fact]
    public Task A_needs_you_row_whose_subject_is_an_instance_address_opens_that_instances_frame_at_its_children() => UiThread.Run(async () =>
    {
        var (address, _) = Served();
        var shell = await Connected(address);
        var known = AnyRow(shell);

        shell.FollowChildAnswerFromNeedsYou(shell.NeedsYou.Seen.Rows[0].Id, OfNeedsYou(shell));

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, address), shell.SheetLink);
        Assert.Equal(address, known.Address);
    }, Token);

    [Fact]
    public Task A_needs_you_row_whose_subject_is_an_instance_id_opens_the_frame_of_the_row_with_that_id() => UiThread.Run(async () =>
    {
        var (_, id) = Served();
        var shell = await Connected(id);
        var known = AnyRow(shell);

        shell.FollowChildAnswerFromNeedsYou(shell.NeedsYou.Seen.Rows[0].Id, OfNeedsYou(shell));

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, known.Address), shell.SheetLink);
        Assert.Equal(id, known.InstanceId);
    }, Token);

    [Fact]
    public Task A_needs_you_row_no_list_knows_opens_its_own_sheet_and_never_a_guessed_child() => UiThread.Run(async () =>
    {
        var shell = await Connected(subject: "WFI-00000000000000000000000000000000");
        var row = shell.NeedsYou.Seen.Rows[0];

        shell.FollowChildAnswerFromNeedsYou(row.Id, OfNeedsYou(shell));

        Assert.NotNull(shell.SheetLink);
        Assert.NotEqual(DeepLinkKind.Workflow, shell.SheetLink.Kind);
    }, Token);

    [Fact]
    public Task A_needs_you_row_served_a_child_opens_that_childs_frame_not_the_parent() => UiThread.Run(async () =>
    {
        var (address, _) = Served();
        var shell = await Connected(address, "park-mirrored-child");
        var child = (string)GoldenFiles.Node("needs-you", "park-mirrored-child")["rows"]![0]!["child"]!;

        shell.FollowChildAnswerFromNeedsYou(shell.NeedsYou.Seen.Rows[0].Id, OfNeedsYou(shell));

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, child), shell.SheetLink);
    }, Token);

    [Fact]
    public Task Following_from_a_strip_row_keeps_that_row_as_the_opener() => UiThread.Run(async () =>
    {
        var shell = await Connected(subject: null);
        var served = FixtureInstances.Summary("escalated-child");

        shell.FollowChildAnswerFromStrip("some-strip-row", ChildAnswerViewModel.ForInstance(served.Address, served.Answer!.Child));

        Assert.Equal("some-strip-row", shell.OpenerStrip);
    }, Token);

    [Fact]
    public Task Following_from_a_Workflows_row_keeps_that_row_as_the_opener() => UiThread.Run(async () =>
    {
        var shell = await Connected(subject: null);
        var row = AnyRow(shell);

        shell.Workflows.FollowChildAnswer(row.Address, ChildAnswerViewModel.ForInstance(row.Address, childAddress: null));

        Assert.Equal(row.Address, shell.OpenerWorkflow);
        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, row.Address), shell.SheetLink);
    }, Token);
}
