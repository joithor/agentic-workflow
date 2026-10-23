using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// 'sindri workflow answer', 'command' and 'cancel' (engine spec rev 8 §5.2, §5.8, §6.2, §6.3) against the pinned dev
/// server: each is an Update with the nonce as its id, validated before it is accepted; an answer that doesn't fit the
/// park is SND-WF-003 and records nothing; a protected answer needs no challenge (owner decision q93); cancel is exempt from the approval check.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ApplyCommandTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private static readonly string _skipRecipe = string.Join('\n', [
        "schema: 1",
        "id: workflow.skip",
        "version: 1",
        "inputs: [{ key: fact:spec, type: text, kind: text }]",
        "initial: produce",
        "states:",
        "  produce: { kind: active, step: step.produce, timeout: { after: 4h, then: park }, next: \"Produce the output.\" }",
        "  done:    { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: produce, to: done, on: step.completed, when: { outcome: done }, produces: [fact:output] }",
        "  - { from: produce, to: done, on: command.skip }",
    ]);

    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Text_on_a_rounds_park_is_SND_WF_003_naming_the_grant_and_records_nothing()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var address = await Rounds(served, "T-11");
        var events = await served.Events(address);

        var text = await served.Cli("answer", address, "--text", "try harder");

        Assert.Equal(2, text.Exit);
        Assert.StartsWith("SND-WF-003 ", text.Stderr, StringComparison.Ordinal);
        Assert.Contains("--grant", text.Stderr, StringComparison.Ordinal);
        Assert.Equal(events, await served.Events(address));
        Assert.Equal(ParkReason.Rounds, (await served.Until(address, static v => v.State == "parked")).Question?.Reason);
    }

    [Fact(Timeout = 120_000)]
    public async Task Grant_with_no_challenge_runs_the_withheld_transition()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var address = await Rounds(served, "T-13");
        _ = await served.Until(address, static v => v.State == "parked");

        var granted = await Apply(served, new ApplyCommandRequest(address, ApplyCommandKind.Answer, AnswerVariant.Grant, Text: null, Amount: 1, Verb: null, Nonce: null, Challenge: null));

        Assert.False(granted.IsError, granted.IsError ? granted.Error.Message : null);
        Assert.Matches(@"^resumed \([a-z]+\): -> produce$", granted.Value.Result.Happened);
        Assert.Equal("produce", granted.Value.Result.Instance.State);
        Assert.Null(granted.Value.Result.Instance.Question);
    }

    [Fact(Timeout = 120_000)]
    public async Task Text_on_a_no_progress_park_resumes_and_the_same_nonce_again_is_already_applied()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var address = await NoProgress(served, "T-14");
        string[] answer = ["answer", address, "--text", "the version comes from the assembly", "--nonce", new string('e', 32)];

        var first = await served.Cli(answer);
        var events = await served.Events(address);
        var again = await served.Cli(answer);

        Assert.Equal((0, string.Empty), (first.Exit, first.Stderr));
        Assert.Matches(@"^T-14/step/1  workflow\.step@[0-9a-f]{12}  v6  produce: desk task  since", first.Lines[0]);
        Assert.Matches(@"^  resumed \([a-z]+\): -> produce$", first.Lines[1]);
        Assert.Equal((0, string.Empty), (again.Exit, again.Stderr));
        Assert.Equal("  already applied (" + first.Lines[1].Trim() + ")", again.Lines[1]);
        Assert.Equal(events, await served.Events(address));
    }

    [Fact(Timeout = 120_000)]
    public async Task Cancel_again_with_its_nonce_after_the_instance_ended_is_already_applied_and_records_nothing()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        _ = await served.Cli("start", "workflow.step", "--item", "T-19", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        _ = await served.Until("T-19/step/1", static v => v.Task is not null);
        string[] cancel = ["cancel", "T-19/step/1", "--nonce", new string('c', 32)];

        var first = await served.Cli(cancel);
        _ = await served.Until("T-19/step/1", static v => v.State == "cancelled");
        var events = await served.Events("T-19/step/1");
        var again = await served.Cli(cancel);

        Assert.Equal((0, string.Empty), (first.Exit, first.Stderr));
        Assert.Equal("  -> cancelled", first.Lines[1]);
        Assert.Equal((0, string.Empty), (again.Exit, again.Stderr));
        Assert.Equal("  already applied (-> cancelled)", again.Lines[1]);
        Assert.Equal(events, await served.Events("T-19/step/1"));
    }

    [Fact(Timeout = 120_000)]
    public async Task Answer_while_nothing_waits_for_one_is_SND_WF_003_and_records_nothing()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        _ = await served.Cli("start", "workflow.step", "--item", "T-15", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        _ = await served.Until("T-15/step/1", static v => v.Task is not null);

        var resume = await served.Cli("answer", "T-15/step/1", "--resume");

        Assert.Equal(2, resume.Exit);
        Assert.StartsWith("SND-WF-003 ", resume.Stderr, StringComparison.Ordinal);
        Assert.Equal(["workflow.started"], await served.Events("T-15/step/1"));
    }

    [Fact(Timeout = 120_000)]
    public async Task Command_fires_the_recipe_command_and_an_unknown_verb_is_SND_WF_003()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        _ = WriteSkip();
        _ = await served.Cli("start", "workflow.skip", "--item", "T-16", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        _ = await served.Until("T-16/skip/1", static v => v.Task is not null);

        var unknown = await served.Cli("command", "T-16/skip/1", "hold");
        var skip = await served.Cli("command", "T-16/skip/1", "skip");

        Assert.Equal(2, unknown.Exit);
        Assert.StartsWith("SND-WF-003 ", unknown.Stderr, StringComparison.Ordinal);
        Assert.Equal((0, string.Empty), (skip.Exit, skip.Stderr));
        Assert.Matches(@"^T-16/skip/1  workflow\.skip@[0-9a-f]{12}  v2  done: resolved  since", skip.Lines[0]);
        Assert.Equal("  -> done", skip.Lines[1]);
        Assert.Equal(["workflow.started", "workflow.transition"], await served.Events("T-16/skip/1"));
    }

    [Fact(Timeout = 120_000)]
    public async Task On_a_revoked_recipe_a_command_is_SND_RECIPE_005_but_cancel_is_exempt()
    {
        var recipe = WriteSkip();
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        _ = await served.Cli("start", "workflow.skip", "--item", "T-17", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        _ = await served.Until("T-17/skip/1", static v => v.Task is not null);
        _harness.Revoke(recipe);   // revoked while its instance runs (W11)

        var skip = await served.Cli("command", "T-17/skip/1", "skip");
        var events = await served.Events("T-17/skip/1");
        var cancel = await served.Cli("cancel", "T-17/skip/1");

        Assert.Equal(2, skip.Exit);
        Assert.StartsWith("SND-RECIPE-005 ", skip.Stderr, StringComparison.Ordinal);
        Assert.Equal(["workflow.started"], events);
        Assert.Equal((0, string.Empty), (cancel.Exit, cancel.Stderr));
        Assert.Equal("  -> cancelled", cancel.Lines[1]);
        Assert.Equal("cancelled", (await served.Until("T-17/skip/1", static v => v.State == "cancelled")).State);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_bare_item_with_roots_of_two_recipes_is_SND_WF_006_listing_them()
    {
        _ = WriteSkip();
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var spec = "fact:spec=" + _harness.File("spec.md", "the spec");
        _ = await served.Cli("start", "workflow.step", "--item", "T-18", "--fact", spec);
        _ = await served.Cli("start", "workflow.skip", "--item", "T-18", "--fact", spec);

        var cancel = await Eventually(served, static run => run.Stderr.StartsWith("SND-WF-006 ", StringComparison.Ordinal), "cancel", "T-18");

        Assert.Equal(2, cancel.Exit);
        Assert.Contains("T-18/skip/1", cancel.Stderr, StringComparison.Ordinal);
        Assert.Contains("T-18/step/1", cancel.Stderr, StringComparison.Ordinal);
        Assert.Equal(["workflow.started"], await served.Events("T-18/step/1"));
    }

    /// <summary>Registers and approves 'workflow.skip' (a desk Step and a 'command.skip' transition) in the home's ledger.</summary>
    private Core.Workflow.Registry.RegisteredRecipe WriteSkip() => _harness.Approve(_skipRecipe);

    private static async Task<Kernel.Result<ApplyCommandResponse>> Apply(Served served, ApplyCommandRequest request)
    {
        using var caller = served.AsHumanAtTerminal();
        await using var scope = served.Sindri.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IService<ApplyCommandRequest, ApplyCommandResponse>>().Handle(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Runs a verb until <paramref name="holds"/> (visibility is eventually consistent).</summary>
    private static async Task<CliRun> Eventually(Served served, Func<CliRun, bool> holds, params string[] args)
    {
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(20);
        var run = await served.Cli(args);
        while (!holds(run) && RealTime.NowDateTime < deadline)
        {
            await Task.Delay(200, TestContext.Current.CancellationToken);
            run = await served.Cli(args);
        }

        return run;
    }

    private static string Numbered(string text, int n) => text + n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Starts 'workflow.step' on <paramref name="item"/> and runs it to a rounds park: three rounds, each with new findings.</summary>
    private async Task<string> Rounds(Served served, string item)
    {
        var address = item + "/step/1";
        _ = await served.Cli("start", "workflow.step", "--item", item, "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        for (var round = 1; round <= 3; round++)
        {
            _ = await served.Report(address, "done", ("fact:output", _harness.File(Numbered("out", round) + ".md", Numbered("output ", round))));
            _ = await served.Report(address, "failed", ("fact:findings", _harness.File(Numbered("findings", round) + ".md", Numbered("findings ", round))));
        }

        Assert.Equal(ParkReason.Rounds, (await served.Until(address, static v => v.State == "parked")).Question?.Reason);
        return address;
    }

    /// <summary>Starts 'workflow.step' on <paramref name="item"/> and runs it to a no-progress park: the same findings twice.</summary>
    private async Task<string> NoProgress(Served served, string item)
    {
        var address = item + "/step/1";
        _ = await served.Cli("start", "workflow.step", "--item", item, "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        var findings = _harness.File("findings.md", "the same findings");
        _ = await served.Report(address, "done", ("fact:output", _harness.File("out1.md", "output 1")));
        _ = await served.Report(address, "failed", ("fact:findings", findings));
        _ = await served.Report(address, "done", ("fact:output", _harness.File("out2.md", "output 2")));
        _ = await served.Report(address, "failed", ("fact:findings", findings));
        Assert.Equal(ParkReason.NoProgress, (await served.Until(address, static v => v.State == "parked")).Question?.Reason);
        return address;
    }
}
