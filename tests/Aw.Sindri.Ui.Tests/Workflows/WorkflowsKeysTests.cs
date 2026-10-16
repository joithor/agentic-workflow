using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The keys on the Workflows page (screens §4.2, U8 T7): J / K across the groups, Enter, A, X, and B and N naming their phase, with the selection kept by address.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsKeysTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IReadOnlyList<InstanceSummary> Served => FixtureInstances.List("open").Instances;

    private static InstanceSummary Desk => Served.First(r => r.Answer is null && r.Actions.Contains("CompleteStep", StringComparer.Ordinal) && r.States.Any(s => s.Desk && string.Equals(s.State, r.State, StringComparison.Ordinal)));

    private static InstanceSummary Locked => Served.First(r => r.Answer is not null && string.Equals(r.Answer.Class, "protected", StringComparison.Ordinal));

    private static InstanceSummary Plain => Served.First(r => r.Answer is null && r.Actions.Contains("ApplyCommand:cancel", StringComparer.Ordinal) && !r.Actions.Contains("CompleteStep", StringComparer.Ordinal));

    private static InstanceDetail Detail(InstanceSummary row) => FixtureInstances.Details.First(d => string.Equals(d.Detail.Id, row.Id, StringComparison.Ordinal)).Detail;

    private async Task<ShellViewModel> OnWorkflows()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Workflows.Groups.Count > 0, Token);
        shell.GoTo(PageId.Workflows);
        return shell;
    }

    private static List<string> Flat(ShellViewModel shell) => [.. shell.Workflows.Groups.SelectMany(g => g.Rows).Select(r => r.Address)];

    private static bool Reached(ShellViewModel shell) => shell.Instance.Phase == FramePhase.Shown;

    // The list the API would serve with its instances reordered or reduced.
    private void Serve(Func<IEnumerable<JsonNode?>, IEnumerable<JsonNode?>> change)
    {
        var body = GoldenFiles.Node("workflow-instances", "open");
        var rows = change([.. body["instances"]!.AsArray()]).Select(r => r?.DeepClone());
        body["instances"] = new JsonArray([.. rows]);
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    [Fact]
    public Task Enter_opens_the_instance_frame() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Press("J"));
        var address = Flat(shell)[0];

        Assert.True(shell.Press("Enter"));
        await ShellWait.Until(shell, () => Reached(shell), Token);

        Assert.Equal((address, address, true), (shell.Instance.Address, shell.OpenerWorkflow, shell.IsInstanceSheet));
    }, Token);

    [Fact]
    public Task A_on_a_desk_task_opens_the_frame_on_the_pending_output() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var asked = new List<(FrameFocus Target, string? Key)>();
        shell.Instance.FocusRequested += (_, e) => asked.Add((e.Target, e.Key));
        var pending = Detail(Desk).Facts.First(f => f.Status == FactStatus.Pending && f.Pending is not null).Key;
        Assert.True(shell.Workflows.Select(Desk.Address));

        Assert.True(shell.Press("A"));
        await ShellWait.Until(shell, () => Reached(shell), Token);

        Assert.Equal(Desk.Address, shell.Instance.Address);
        Assert.Equal([(FrameFocus.PendingOutput, pending)], asked);
    }, Token);

    [Fact]
    public Task A_on_a_protected_answer_opens_the_lock_popover() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Locked.Address));
        var row = shell.Workflows.SelectedRow!;

        Assert.True(shell.Press("A"));

        Assert.Equal((Locked.Answer!.Cli, true, false), (row.Protected?.Cli, row.Protected?.IsOpen, shell.IsSheetOpen));
    }, Token);

    [Fact]
    public Task A_on_a_row_with_nothing_to_answer_does_nothing() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Plain.Address));

        Assert.False(shell.Press("A"));
        Assert.False(shell.IsSheetOpen);
    }, Token);

    [Fact]
    public Task X_shows_the_served_confirm_and_Keep_sends_nothing() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var served = Detail(Plain).Current.Status.Commands.First(c => c.Verb == StatusVerb.Cancel).Confirm;
        Assert.True(shell.Workflows.Select(Plain.Address));

        Assert.True(shell.Press("X"));
        await ShellWait.Until(shell, () => Reached(shell) && shell.Instance.Expanded is { IsConfirmingCancel: true }, Token);

        var expanded = shell.Instance.Expanded!;
        Assert.Equal(served, expanded.ConfirmLine);
        expanded.Keep();
        Assert.Equal((false, string.Empty), (expanded.IsConfirmingCancel, expanded.PendingNote));
    }, Token);

    [Fact]
    public Task B_and_N_say_arrive_with_their_phase() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Plain.Address));

        Assert.True(shell.Press("B"));
        Assert.Equal(Labels.HandBackArrives, shell.Workflows.KeyNote);
        Assert.True(shell.Press("N"));
        Assert.Equal(Labels.StartArrives, shell.Workflows.KeyNote);
        Assert.False(shell.IsSheetOpen);

        Assert.True(shell.Press("J"));
        Assert.Equal(string.Empty, shell.Workflows.KeyNote);
    }, Token);

    [Fact]
    public Task J_K_move_across_groups() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var flat = Flat(shell);
        Assert.True(shell.Workflows.Groups.Count > 1);

        var forward = new List<string?>();
        foreach (var _ in flat)
        {
            Assert.True(shell.Press("J"));
            forward.Add(shell.Workflows.SelectedAddress);
        }

        Expect.Sequence(flat, forward);
        Assert.True(shell.Press("J"));
        Assert.Equal(flat[^1], shell.Workflows.SelectedAddress);

        var back = new List<string?>();
        for (var i = flat.Count - 2; i >= 0; i--)
        {
            Assert.True(shell.Press("K"));
            back.Add(shell.Workflows.SelectedAddress);
        }

        Expect.Sequence(flat.AsEnumerable().Reverse().Skip(1), back);
        Assert.Equal(1, shell.Workflows.Groups.SelectMany(g => g.Rows).Count(r => r.IsSelected));
    }, Token);

    [Fact]
    public Task J_skips_the_rows_of_a_collapsed_group() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var second = shell.Workflows.Groups[1];
        var first = shell.Workflows.Groups[0].Rows[^1].Address;
        second.Toggle();
        Assert.True(shell.Workflows.Select(first));

        Assert.True(shell.Press("J"));

        Assert.Equal(shell.Workflows.Groups[2].Rows[0].Address, shell.Workflows.SelectedAddress);
    }, Token);

    [Fact]
    public Task Selection_is_kept_by_address_across_refreshes() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        var keep = Flat(shell)[3];
        Assert.True(shell.Workflows.Select(keep));

        Serve(rows => rows.Reverse());
        await shell.Workflows.Refresh(_api, Token);

        Assert.Equal(keep, shell.Workflows.SelectedAddress);
        Expect.Sequence([keep], shell.Workflows.Groups.SelectMany(g => g.Rows).Where(r => r.IsSelected).Select(r => r.Address));
    }, Token);

    [Fact]
    public Task Keys_never_act_on_a_row_that_went_or_that_the_user_left() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Desk.Address));
        var left = shell.Workflows.SelectedRow!;
        Assert.True(shell.Workflows.Select(Locked.Address));
        Assert.Equal((false, true), (left.IsSelected, shell.Workflows.SelectedRow!.IsSelected));

        Serve(rows => rows.Where(r => !string.Equals((string)r!["address"]!, Locked.Address, StringComparison.Ordinal)));
        await shell.Workflows.Refresh(_api, Token);

        Assert.Null(shell.Workflows.SelectedAddress);
        Assert.False(shell.Press("Enter"));
        Assert.False(shell.Press("A"));
        Assert.False(shell.Press("X"));
        Assert.False(shell.IsSheetOpen);
    }, Token);

    [Fact]
    public Task The_keys_belong_to_the_page_that_is_showing() => UiThread.Run(async () =>
    {
        var shell = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Plain.Address));
        shell.GoTo(PageId.Home);

        _ = shell.Press("Enter");

        Assert.False(shell.IsInstanceSheet);
    }, Token);
}
